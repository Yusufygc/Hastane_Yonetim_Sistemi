# Ödev Gereksinimleri

Kaynak (ham): `ProjeOdevi_Son.docx`. Seçenek A: Hastane/Klinik Randevu Sistemi, en fazla 2 kişi. Mimari özeti: [[mimari]]. Fazlar: [[faz-plani]].

## Zorunlu gereksinimler
- **Hastane/klinik tanımları:** Yetki çerçevesinde tanımlanabilir; tek klinik ve çok şubeli yapı desteklenir.
- **Çalışma saatleri ve bölümler** her kurum için tanımlanabilir.
- **Hizmetler:** Ad, süre ve ücret ile tanımlanır (genel muayene, radyoloji, laboratuvar vb.).
- **Çalışan yönetimi:** Doktor ve sağlık personeli kaydı; doktorun uzmanlığı ve sunduğu hizmetler.
- **Uygunluk:** Doktorların uygunluk dilimleri tanımlanır; hasta yalnız bu aralıklara randevu alır.
- **Randevu:** Uygun çalışan ve işlem seçilerek oluşturulur; **çakışma kontrolü** yapılır; detaylar (işlem, süre, ücret, çalışan, tarih/saat) kaydedilir; **onay mekanizması** vardır.
- **Roller:** En az üç — Hasta, Doktor/Sağlık Personeli, Hastane/Klinik Yöneticisi.

## Teslim kuralları
- GitHub'da **en az 10 commit, farklı günlerde** ([[git-is-akisi]]); ayrıca Sabis'e yükleme.
- Sunumlar son 4 haftada; sunuma katılmayan puan alamaz.
- **Seçim bildirimi son tarih: 13 Ekim 2026** (Google Sheet'e girilmeli).

## Gereksinim → Çözüm
| Gereksinim | Çözüm | Detay |
|---|---|---|
| Kurum, çok şube | `ORGANIZATION → HOSPITAL` | [[veritabani]] |
| Çalışma saatleri | `HOSPITAL_WORKING_HOURS` | [[veritabani]] |
| Hizmet süre/ücret | `SERVICE` | [[veritabani]] |
| Doktor-hizmet | `DOCTOR_SERVICE` (yönetici onaylı) | [[randevu-kurallari]] |
| Uygunluk | `DOCTOR_AVAILABILITY`, `DOCTOR_TIME_OFF`, slot üretimi | [[randevu-kurallari]] |
| Çakışma kontrolü | Uygulama + DB exclusion constraint | [[randevu-kurallari]] |
| Onay | Doktor onaylar/reddeder | [[randevu-kurallari]] |
| Roller | Patient, Doctor (+Staff), Admin | [[kimlik-ve-yetki]] |
| OOP ilkeleri | `User` hiyerarşisi (TPT), State pattern | [[domain-modeli]] |
