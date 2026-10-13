#!/usr/bin/env bash
# Çalışan API'yi uçtan uca dener (JWT Bearer): kayıt → giriş → me → yetki → refresh/rotation → çıkış → yönetici.
#
# Kullanım (API çalışırken; Git Bash / WSL / Linux):
#   ./scripts/smoke-test.sh                          # https://localhost:7032 (geliştirme sertifikası için -k)
#   BASE=http://localhost:5284 ./scripts/smoke-test.sh
#
# Her çalıştırma benzersiz e-posta ve geçerli bir T.C. kimlik numarası üretir (geliştirme veritabanına bir test hastası ekler).
# Süper yönetici bilgileri geliştirme ayar dosyasından (Seed:SuperAdmin) okunur; bulunamazsa yönetici adımları atlanır.
# Not: register/login/refresh uçları IP başına dakikada 10 istekle sınırlıdır; bu betik ~9 istek yapar,
#      aralıksız iki kez çalıştırmayın (429 alırsınız).
set -u

BASE="${BASE:-https://localhost:7032}"
SETTINGS="$(dirname "$0")/../src/Presentation/Hospital.WebAPI/appsettings.Development.json"

PASS=0
FAIL=0
LAST_BODY=""
LAST_STATUS=""

check() { # check <açıklama> <beklenen_kod>
  if [ "$LAST_STATUS" = "$2" ]; then
    echo "  [OK]   $1 -> $LAST_STATUS"
    PASS=$((PASS + 1))
  else
    echo "  [FAIL] $1 -> $LAST_STATUS (beklenen $2) :: $LAST_BODY"
    FAIL=$((FAIL + 1))
  fi
}

# call <erişim-belirteci|-> <METHOD> <yol> [json-gövde]
call() {
  local token="$1" method="$2" path="$3" body="${4:-}"
  local args=(-sk -m 20 -X "$method" -w $'\n%{http_code}')

  if [ "$token" != "-" ] && [ -n "$token" ]; then
    args+=(-H "Authorization: Bearer $token")
  fi

  local out
  if [ -n "$body" ]; then
    # Gövde komut satırı argümanı yerine stdin ile gönderilir: Windows'ta argümanlar ANSI kod sayfasına çevrilir
    # ve Türkçe karakterler (ş, ı, ğ...) geçersiz UTF-8 olarak 400 döndürür. Pipe baytları olduğu gibi taşır.
    out="$(printf '%s' "$body" | curl "${args[@]}" -H 'Content-Type: application/json' --data-binary @- "$BASE$path")"
  else
    out="$(curl "${args[@]}" "$BASE$path")"
  fi

  LAST_STATUS="${out##*$'\n'}"
  LAST_BODY="${out%$'\n'*}"
}

# Geçerli (sağlama basamaklı) rastgele T.C. kimlik numarası üretir.
new_identity_number() {
  local n=$((100000000 + (RANDOM * RANDOM) % 800000000))
  local d=() i
  for ((i = 0; i < 9; i++)); do d+=("${n:i:1}"); done
  local odd=$((d[0] + d[2] + d[4] + d[6] + d[8]))
  local even=$((d[1] + d[3] + d[5] + d[7]))
  local tenth=$(((((odd * 7 - even) % 10) + 10) % 10))
  local sum=$((odd + even + tenth))
  echo "${n}${tenth}$((sum % 10))"
}

json_value() { # json_value <anahtar> → LAST_BODY içinden ilk eşleşen değer
  echo "$LAST_BODY" | sed -nE "s/.*\"$1\":\"?([^\",}]*)\"?.*/\1/p" | head -n1
}

login() { # login <e-posta> <parola> → ACCESS / REFRESH değişkenlerini doldurur
  call - POST /api/auth/login "{\"email\":\"$1\",\"password\":\"$2\"}"
  ACCESS="$(json_value accessToken)"
  REFRESH="$(json_value refreshToken)"
}

EMAIL="smoke.$(date +%s)@example.test"
PASSWORD="Smoke1234x"
TC="$(new_identity_number)"
ACCESS=""
REFRESH=""

echo "Hedef: $BASE"
echo
echo "1) Sağlık ve herkese açık katalog (token gerekmez)"
call - GET /health
check "GET /health" 200
call - GET /api/catalog/hospitals
check "GET /api/catalog/hospitals (anonim)" 200
HOSPITAL_ID="$(echo "$LAST_BODY" | sed -nE 's/.*"id":"([^"]+)".*/\1/p' | head -n1)"
if [ -n "$HOSPITAL_ID" ]; then
  call - GET "/api/catalog/hospitals/$HOSPITAL_ID/services"
  check "GET /api/catalog/hospitals/{id}/services (anonim)" 200
fi

echo
echo "2) Hasta kaydı ve giriş  ($EMAIL)"
BODY=$(printf '{"email":"%s","password":"%s","firstName":"Ayşe","lastName":"Yılmaz","identityNumber":"%s","birthDate":"1990-05-17","phone":null}' "$EMAIL" "$PASSWORD" "$TC")
call - POST /api/auth/register "$BODY"
check "POST /api/auth/register" 201
call - POST /api/auth/register "$BODY"
check "Aynı e-posta ile ikinci kayıt" 409
call - POST /api/auth/login "{\"email\":\"$EMAIL\",\"password\":\"yanlis-Parola1\"}"
check "Yanlış parola ile giriş" 401
login "$EMAIL" "$PASSWORD"
check "POST /api/auth/login" 200
echo "         accessToken: ${ACCESS:0:25}...  (JWT, kısa ömürlü)"
call "$ACCESS" GET /api/auth/me
check "GET /api/auth/me (Bearer ile)" 200
echo "         rol: $(json_value role)"

echo
echo "3) Yetki ve token doğrulama"
call - GET /api/auth/me
check "Token olmadan GET /api/auth/me" 401
call "bozuk.token.degeri" GET /api/auth/me
check "Bozuk token ile GET /api/auth/me" 401
call "$ACCESS" GET /api/hospitals
check "Hasta -> GET /api/hospitals" 403
call "$ACCESS" GET /api/organization
check "Hasta -> GET /api/organization" 403

echo
echo "4) Yenileme belirteci (refresh) ve çıkış"
OLD_REFRESH="$REFRESH"
call - POST /api/auth/refresh "{\"refreshToken\":\"$REFRESH\"}"
check "POST /api/auth/refresh (yeni token çifti)" 200
ACCESS="$(json_value accessToken)"
REFRESH="$(json_value refreshToken)"
call "$ACCESS" GET /api/auth/me
check "Yenilenen token ile GET /api/auth/me" 200
call - POST /api/auth/refresh "{\"refreshToken\":\"$OLD_REFRESH\"}"
check "Eski (kullanılmış) refresh token tekrar -> reddedilir" 401
call "$ACCESS" POST /api/auth/logout "{\"refreshToken\":\"$REFRESH\"}"
check "POST /api/auth/logout" 204

echo
echo "5) Yönetici (seed ile gelen süper yönetici)"
ADMIN_EMAIL="${ADMIN_EMAIL:-$(sed -nE 's/.*"Email": *"([^"]+)".*/\1/p' "$SETTINGS" 2>/dev/null | head -n1)}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-$(sed -nE 's/.*"Password": *"([^"]+)".*/\1/p' "$SETTINGS" 2>/dev/null | head -n1)}"
if [ -n "$ADMIN_EMAIL" ] && [ -n "$ADMIN_PASSWORD" ]; then
  login "$ADMIN_EMAIL" "$ADMIN_PASSWORD"
  check "Süper yönetici girişi" 200
  call "$ACCESS" GET /api/hospitals
  check "GET /api/hospitals (yönetici)" 200
  call "$ACCESS" GET /api/organization
  check "GET /api/organization (süper yönetici)" 200
  call "$ACCESS" GET "/api/services?hospitalId=$HOSPITAL_ID"
  check "GET /api/services?hospitalId= (yönetici)" 200
else
  echo "  (atlandı: ADMIN_EMAIL/ADMIN_PASSWORD verilmedi ve ayar dosyasında Seed:SuperAdmin bulunamadı)"
fi

echo
echo "Sonuç: $PASS başarılı, $FAIL başarısız"
[ "$FAIL" -eq 0 ]
