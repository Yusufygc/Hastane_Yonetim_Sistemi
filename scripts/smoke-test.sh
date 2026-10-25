#!/usr/bin/env bash
# Çalışan API'yi uçtan uca dener (JWT Bearer): kayıt → giriş → me → yetki → refresh/rotation → çıkış → yönetici → doktor/profil → randevu akışı → e-posta bildirimleri (Mailpit).
#
# Kullanım (API çalışırken; Git Bash / WSL / Linux):
#   ./scripts/smoke-test.sh                          # https://localhost:7032 (geliştirme sertifikası için -k)
#   BASE=http://localhost:5284 ./scripts/smoke-test.sh
#
# Her çalıştırma benzersiz e-posta ve geçerli bir T.C. kimlik numarası üretir (geliştirme veritabanına bir test hastası ekler).
# Süper yönetici bilgileri geliştirme ayar dosyasından (Seed:SuperAdmin) okunur; bulunamazsa yönetici adımları atlanır.
# Not: register/login/refresh uçları IP başına dakikada 10 istekle sınırlıdır (Development ayarında 100); bu betik ~15 istek yapar,
#      Production ayarlarıyla aralıksız iki kez çalıştırmayın (429 alırsınız).
# Doktor adımları Seed:SampleDoctor (geliştirme) hesabını kullanır; DOCTOR_EMAIL/DOCTOR_PASSWORD ile değiştirilebilir.
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

first_match() { # first_match <regex> → LAST_BODY içindeki ilk eşleşmenin tamamı
  echo "$LAST_BODY" | grep -oE "$1" | head -n1
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
echo "6) Faz 3: profil, doktor ve çalışan uçları"
# Hasta profili: T.C. maskeli döner.
login "$EMAIL" "$PASSWORD"
PATIENT_ACCESS="$ACCESS"
call "$PATIENT_ACCESS" GET /api/patients/me
check "GET /api/patients/me (hasta)" 200
echo "         maskeli T.C.: $(json_value maskedIdentityNumber)"
call "$PATIENT_ACCESS" PUT /api/patients/me '{"firstName":"Ayşe","lastName":"Yılmaz","phone":"05551112233","bloodType":"A+"}'
check "PUT /api/patients/me (kan grubu A+)" 200
call "$PATIENT_ACCESS" PUT /api/patients/me '{"firstName":"Ayşe","lastName":"Yılmaz","bloodType":"Z+"}'
check "PUT /api/patients/me geçersiz kan grubu" 400
call "$PATIENT_ACCESS" GET /api/doctors/me
check "Hasta -> GET /api/doctors/me" 403
call "$PATIENT_ACCESS" GET /api/doctors
check "Hasta -> GET /api/doctors (yönetici ucu)" 403

# Katalogdaki doktorlar (anonim): yalnız onaylı hizmeti olan aktif doktorlar, kişisel veri yok.
if [ -n "$HOSPITAL_ID" ]; then
  call - GET "/api/catalog/doctors?hospitalId=$HOSPITAL_ID"
  check "GET /api/catalog/doctors (anonim)" 200
  call - GET "/api/catalog/doctors"
  check "GET /api/catalog/doctors hospitalId olmadan" 400
fi

# Örnek doktor (seed) kendi profilini, uygunluğunu ve hizmetlerini görür.
DOCTOR_EMAIL="${DOCTOR_EMAIL:-doktor@hastane.local}"
DOCTOR_PASSWORD="${DOCTOR_PASSWORD:-Doktor12345x}"
login "$DOCTOR_EMAIL" "$DOCTOR_PASSWORD"
if [ -n "$ACCESS" ]; then
  DOCTOR_ACCESS="$ACCESS"
  call "$DOCTOR_ACCESS" GET /api/doctors/me
  check "GET /api/doctors/me (doktor)" 200
  call "$DOCTOR_ACCESS" GET /api/doctors/me/availability
  check "GET /api/doctors/me/availability" 200
  call "$DOCTOR_ACCESS" GET /api/doctors/me/services
  check "GET /api/doctors/me/services" 200
  call "$DOCTOR_ACCESS" GET /api/patients/me
  check "Doktor -> GET /api/patients/me" 403
  call "$DOCTOR_ACCESS" POST /api/doctors/me/availability '{"day":0,"startTime":"09:00","endTime":"12:00","validFrom":"2026-01-01"}'
  check "Doktor: şube kapalıyken (Pazar) uygunluk ekleme" 400
else
  echo "  (atlandı: örnek doktor ile giriş yapılamadı; Seed:SampleDoctor ve Seed:SampleData açık mı?)"
fi

# Yönetici: doktor ve personel listeleri, bekleyen talepler.
if [ -n "${ADMIN_EMAIL:-}" ] && [ -n "${ADMIN_PASSWORD:-}" ]; then
  login "$ADMIN_EMAIL" "$ADMIN_PASSWORD"
  call "$ACCESS" GET /api/doctors
  check "GET /api/doctors (yönetici)" 200
  call "$ACCESS" GET /api/staff
  check "GET /api/staff (yönetici)" 200
  call "$ACCESS" GET /api/admins
  check "GET /api/admins (süper yönetici)" 200
  call "$ACCESS" GET "/api/doctor-service-requests?status=pending"
  check "GET /api/doctor-service-requests (yönetici)" 200
  call "$ACCESS" GET "/api/doctor-service-requests?status=bekliyor"
  check "Geçersiz durum filtresi" 400
fi

echo
echo "7) Faz 4: slot sorgusu ve randevu yaşam döngüsü (örnek doktor + Genel Muayene)"
if [ -n "$HOSPITAL_ID" ] && [ -n "${DOCTOR_ACCESS:-}" ]; then
  call - GET "/api/catalog/hospitals/$HOSPITAL_ID/services"
  SERVICE_ID="$(echo "$LAST_BODY" | sed -nE 's/.*"id":"([^"]+)","departmentId":"[^"]*","name":"Genel Muayene".*/\1/p' | head -n1)"
  call - GET "/api/catalog/doctors?hospitalId=$HOSPITAL_ID&serviceId=$SERVICE_ID"
  DOCTOR_ID="$(first_match '"id":"[^"]+"' | sed -E 's/"id":"([^"]+)"/\1/')"

  # En az 3 gün sonrası, hafta içi (örnek doktor Pzt-Cum 09:00-12:00 uygun).
  DAYS=3
  while [ "$(date -d "+$DAYS days" +%u)" -ge 6 ]; do DAYS=$((DAYS + 1)); done
  APPT_DATE="$(date -d "+$DAYS days" +%F)"

  if [ -n "$SERVICE_ID" ] && [ -n "$DOCTOR_ID" ]; then
    call - GET "/api/catalog/doctors/$DOCTOR_ID/slots?serviceId=$SERVICE_ID&date=$APPT_DATE"
    check "GET /api/catalog/doctors/{id}/slots (anonim, $APPT_DATE)" 200
    SLOT_START="$(first_match '"startAt":"[^"]+"' | sed -E 's/"startAt":"([^"]+)"/\1/')"
    echo "         ilk müsait slot (UTC): $SLOT_START"
    call - GET "/api/catalog/doctors/$DOCTOR_ID/slots?serviceId=$SERVICE_ID&date=02.11.2026"
    check "Slot sorgusu geçersiz tarih biçimi (02.11.2026)" 400

    if [ -n "$SLOT_START" ]; then
      call "$PATIENT_ACCESS" POST /api/appointments "{\"doctorId\":\"$DOCTOR_ID\",\"serviceId\":\"$SERVICE_ID\",\"startAt\":\"$SLOT_START\",\"patientNote\":\"Smoke test\"}"
      check "POST /api/appointments (hasta, slot)" 201
      APPT_ID="$(json_value id)"
      call "$PATIENT_ACCESS" POST /api/appointments "{\"doctorId\":\"$DOCTOR_ID\",\"serviceId\":\"$SERVICE_ID\",\"startAt\":\"$SLOT_START\"}"
      check "Aynı slot için ikinci talep" 409
      call "$PATIENT_ACCESS" GET "/api/appointments/$APPT_ID"
      check "GET /api/appointments/{id} (hasta, kendi randevusu)" 200
      echo "         durum: $(json_value status)"
      call "$PATIENT_ACCESS" POST "/api/appointments/$APPT_ID/approve"
      check "Hasta -> onaylama" 403
      call "$DOCTOR_ACCESS" POST "/api/appointments/$APPT_ID/reject" '{"reason":""}'
      check "Doktor -> nedensiz red" 400
      call "$DOCTOR_ACCESS" POST "/api/appointments/$APPT_ID/approve"
      check "Doktor -> onaylama" 204
      call "$DOCTOR_ACCESS" POST "/api/appointments/$APPT_ID/complete"
      check "Doktor -> başlamamış randevuyu tamamlama" 409
      call "$PATIENT_ACCESS" POST "/api/appointments/$APPT_ID/cancel" '{"reason":"Smoke test iptali"}'
      check "Hasta -> iptal (süre sınırı dışında)" 204
      call "$PATIENT_ACCESS" POST "/api/appointments/$APPT_ID/cancel" '{"reason":"tekrar"}'
      check "İptal edilmiş randevuyu tekrar iptal" 409
      call "$PATIENT_ACCESS" GET "/api/patients/me/appointments?status=CANCELLED"
      check "GET /api/patients/me/appointments" 200
      call "$DOCTOR_ACCESS" GET "/api/doctors/me/appointments"
      check "GET /api/doctors/me/appointments (doktor)" 200
      call "$DOCTOR_ACCESS" GET "/api/doctors/me/schedule"
      check "GET /api/doctors/me/schedule (doktor)" 200
    fi
  else
    echo "  (atlandı: örnek doktor/hizmet katalogda bulunamadı; Seed:SampleData ve Seed:SampleDoctor açık mı?)"
  fi

  if [ -n "${ADMIN_EMAIL:-}" ] && [ -n "${ADMIN_PASSWORD:-}" ]; then
    login "$ADMIN_EMAIL" "$ADMIN_PASSWORD"
    call "$ACCESS" GET "/api/appointments?hospitalId=$HOSPITAL_ID"
    check "GET /api/appointments (yönetici)" 200
    call "$PATIENT_ACCESS" GET "/api/appointments"
    check "Hasta -> GET /api/appointments (yönetici ucu)" 403
  fi
else
  echo "  (atlandı: şube veya örnek doktor oturumu yok)"
fi

echo
echo "8) Faz 5: e-posta bildirimleri (Mailpit; randevu akışı çalıştıysa)"
MAILPIT_URL="${MAILPIT_URL:-http://localhost:8025}"
if [ -n "${APPT_ID:-}" ] && curl -s -m 3 -o /dev/null "$MAILPIT_URL/api/v1/info"; then
  # Outbox işleyici birkaç saniyede bir çalışır; Mailpit'e düşmesini bekle (en fazla ~20 sn).
  wait_mail() { # wait_mail <alıcı> <konu parçası>
    local i body
    for i in $(seq 1 20); do
      body="$(curl -s -m 5 "$MAILPIT_URL/api/v1/search?query=to:%22$1%22")"
      if echo "$body" | grep -q "$2"; then return 0; fi
      sleep 1
    done
    return 1
  }
  mail_check() { # mail_check <açıklama> <alıcı> <konu parçası>
    if wait_mail "$2" "$3"; then
      echo "  [OK]   $1"
      PASS=$((PASS + 1))
    else
      echo "  [FAIL] $1 (Mailpit'te bulunamadı: $2 / $3)"
      FAIL=$((FAIL + 1))
    fi
  }
  mail_check "Hasta: 'Randevu talebiniz alındı' e-postası" "$EMAIL" "Randevu talebiniz alındı"
  mail_check "Doktor: 'Yeni randevu talebi' e-postası" "$DOCTOR_EMAIL" "Yeni randevu talebi"
  mail_check "Doktor: hasta iptali e-postası" "$DOCTOR_EMAIL" "Bir randevu iptal edildi"
  mail_check "Hasta: 'Randevunuz onaylandı' e-postası" "$EMAIL" "Randevunuz onaylandı"
else
  echo "  (atlandı: randevu oluşturulmadı veya Mailpit ($MAILPIT_URL) çalışmıyor; 'docker compose up -d' ile açın)"
fi

echo
echo "Sonuç: $PASS başarılı, $FAIL başarısız"
[ "$FAIL" -eq 0 ]
