# Bildirimler (E-posta)

Faz 5 (2026-10-23). Kaynak: `plan.md` §8.4. Kalıp özeti: [[tasarim-kaliplari]] (Transactional Outbox). Tablo: [[veritabani]]. Randevu kuralları: [[randevu-kurallari]]. Kararlar: [[kararlar]] K-57…K-61. Testler: [[test-stratejisi]].

## Akış
```
Appointment.Request/Approve/Reject/Cancel   → domain event (aggregate içinde)
  └─ AppDbContext.SaveChangesAsync           → olayları toplar, aggregate'ten temizler, IDomainEventDispatcher ile dağıtır
       └─ *EmailHandler (IDomainEventHandler) → alıcıları okur, şablonu doldurur, IEmailOutbox ile email_outbox satırı ekler
            └─ base.SaveChangesAsync          → randevu + e-posta satırları TEK transaction'da commit (veya birlikte rollback)
EmailOutboxProcessor (BackgroundService)      → bekleyen satırı kilitler (SKIP LOCKED) → MailKit ile SMTP → SENT / yeniden dene / FAILED
```
İstek yolunda ağ çağrısı yoktur: SMTP yavaş veya kapalı olsa da randevu işlemi etkilenmez.

## Olay → e-posta
| Olay | Alıcı | Konu |
|---|---|---|
| Talep (`AppointmentRequestedEvent`) | Hasta | Randevu talebiniz alındı |
| | Doktor | Yeni randevu talebi onayınızı bekliyor |
| Onay (`AppointmentApprovedEvent`) | Hasta | Randevunuz onaylandı |
| Red (`AppointmentRejectedEvent`) | Hasta (red nedeniyle) | Randevu talebiniz onaylanmadı |
| İptal (`AppointmentCancelledEvent`) | **Karşı taraf**: hasta iptal → doktor; doktor iptal → hasta; yönetici iptal → ikisi | Randevunuz iptal edildi / Bir randevu iptal edildi |

- Şablonlar `Application/Features/Notifications/AppointmentEmails` içinde (Türkçe, **düz metin**). İçerik: hizmet, doktor, şube, tarih/saat (**şubenin saat diliminde**, ör. `02.11.2026 09:30 (Europe/Istanbul)`), varsa red/iptal nedeni. Hasta notu gibi serbest metin ve e-posta adresleri gövdeye konmaz.
- Olaylar yalnız kimlik/hizmet adı/zaman taşır; alıcı adı ve adresi dağıtım sırasında `INotificationReader` ile okunur. (Talep olayı anında randevu henüz DB'de değildir; bu yüzden randevudan değil kişi/şube tablolarından okunur.)

## Outbox (`email_outbox`)
- `dedup_key = {olayId}:{alıcı}` (benzersiz indeks + `EnqueueAsync` ön kontrolü): aynı olay iki kez dağıtılsa da aynı alıcıya **tek** e-posta.
- Durum: `PENDING` → `SENT` veya (deneme sınırı aşılırsa) `FAILED`. `attempt_count`, `last_error` (≤ 1000 karakter), `next_attempt_at`, `sent_at`.
- **Geri çekilme:** başarısız denemeden sonra `next_attempt_at = şimdi + BaseDelay · 2^(deneme−1)` (30, 60, 120, 240 sn…), `MaxDelay` (1 sa) ile sınırlı; 5. başarısızlıkta `FAILED` (otomatik yeniden denenmez).
- **İşleme güvenliği:** `FOR UPDATE SKIP LOCKED` ile sıradaki hazır satır kilitlenir; gönderim sonucu aynı transaction'da yazılır. Birden çok uygulama örneği aynı mesajı göndermez. Gönderimden sonra ama commit'ten önce çökme nadiren tekrar gönderime yol açar (en az bir kez teslim).
- Uygulama kapanırken işlenmekte olan mesaj geri alınır, `PENDING` kalır ve sonraki çalıştırmada gider.

## Yapılandırma (`Email` bölümü)
| Anahtar | Varsayılan | Not |
|---|---|---|
| `Email:Smtp:Host` / `Port` | `localhost` / `1025` | Mailpit. Üretimde gerçek SMTP |
| `Email:Smtp:Security` | `None` | `None` \| `StartTls` \| `Ssl`. **Üretimde `StartTls` veya `Ssl` kullanın** |
| `Email:Smtp:UserName` / `Password` | boş | Parola yalnız ortam değişkeni: `Email__Smtp__Password` (depoya konmaz) |
| `Email:Smtp:FromAddress` / `FromName` | `noreply@hastane.local` / `Hastane Randevu Sistemi` | |
| `Email:Outbox:Enabled` | `true` | `false` → işleyici başlamaz, mesajlar birikir |
| `Email:Outbox:PollSeconds` | `5` | Boşken yoklama aralığı |
| `Email:Outbox:BatchPerCycle` | `20` | Bir turda en fazla mesaj |
| `Email:Outbox:MaxAttempts` / `BaseDelaySeconds` / `MaxDelaySeconds` | `5` / `30` / `3600` | Yeniden deneme politikası |

## Yerelde görme ve hata ayıklama
- `docker compose up -d` → Mailpit arayüzü **http://localhost:8025**. API'yi çalıştırıp randevu işlemi yapınca e-postalar birkaç saniyede düşer. Gönderim arka planda olduğundan API yanıtı e-postayı beklemez.
- Mailpit/SMTP kapalıysa e-postalar `PENDING` kalır; SMTP açılınca (geri çekilme süresi dolunca) gider. Durum: `SELECT status, attempt_count, last_error, next_attempt_at FROM email_outbox ORDER BY created_at DESC;`
- `FAILED` bir mesajı yeniden denetmek için (SMTP düzeldikten sonra): `UPDATE email_outbox SET status='PENDING', attempt_count=0, next_attempt_at=now() WHERE status='FAILED';`
- Loglar yalnız outbox kimliği ve sonucu içerir; alıcı adresi veya gövde loglanmaz.

## Bilinçli sınırlar
- Yalnız randevu olayları için bildirim vardır (hesap davetiyesi, parola sıfırlama e-postası yok; yönetici hesap açarken parolayı verir — [[kimlik-ve-yetki]]).
- Düz metin şablon; HTML, ek dosya, abonelikten çıkma yok.
- Outbox'ı izlemek için yönetici ucu yok (SQL ile bakılır); gerekirse Faz 6'da eklenebilir.
- Hatırlatma e-postası (randevudan önce) kapsam dışı.

## Kod haritası
- Domain: `Entities/Notifications/EmailOutboxMessage` (`MarkSent`, `RegisterFailure`, `RetryPolicy`), zenginleştirilmiş randevu olayları.
- Application: `Interfaces/IDomainEventHandler`, `Features/Notifications/` (`IEmailOutbox`, `IEmailOutboxStore`, `IEmailSender`, `INotificationReader`, ayar kayıtları, `AppointmentEmails`, `EventHandlers/*`).
- Persistence: `Events/DomainEventDispatcher`, `Features/Notifications/` (`EmailOutbox`, `EmailOutboxStore` — `SKIP LOCKED`, `NotificationReader`, EF konfigürasyonu), `AppDbContext` olay dağıtımı, migration `EmailOutbox`.
- Infrastructure: `Features/Notifications/MailKitEmailSender`, `EmailOutboxProcessor` (BackgroundService; `InfrastructureExt` kaydı).
- WebAPI: `WebApiExt` ayarları `Email` bölümünden okur.
