# Test Stratejisi

Kaynak: `plan.md` §11. Faz bağlantısı: [[faz-plani]].

| Katman | Araç | Kapsam |
|---|---|---|
| Unit | xUnit, FluentAssertions | State geçişleri, slot üretimi (pencere × çalışma saati × izin), snapshot, iptal süresi, doktor-hizmet onayı ([[randevu-kurallari]]) |
| Entegrasyon | Gerçek PostgreSQL (compose / CI service; geçici DB) | Exclusion constraint, EF eşlemeleri, outbox |
| Eşzamanlılık | Gerçek PostgreSQL | N paralel istek aynı slota → **tam 1 başarılı, N-1 `409`** |
| Mimari | NetArchTest | Katman/feature kuralları ([[mimari]]) |
| API / Yetki | `WebApplicationFactory` | Endpoint tarama, rol×uç matrisi, IDOR, cookie/CSRF ([[kimlik-ve-yetki]]) |

## Zorunlu çakışma senaryoları
Birebir aynı aralık; kısmi kesişim (baş/son/içinde/kapsayan); bitişik aralıklar (çakışmaz); farklı doktor aynı saat (çakışmaz); iptal/red sonrası slot yeniden alınabilir; aynı hasta iki doktorda aynı saat; DST geçiş günü; gün sonu (23:xx); uygunluk dışı / izin günü / hastane kapalı saat; `PENDING` doktor-hizmetiyle randevu talebi (reddedilmeli).

## Ortam
Entegrasyon testleri `docker compose up -d` ile çalışan PostgreSQL gerektirir ([[docker-ortam]]). Testcontainers **kullanılmıyor**: Smart App Control imzasız DLL'i engelliyor ([[kararlar]] K-20).
