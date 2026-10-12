# Tasarım Kalıpları

Kaynak: `plan.md` §8. Katman yerleşimi: [[mimari]].

## Repository + Unit of Work (yazma tarafı)
- Repository **aggregate root başına**; soyutlama Application feature klasöründe, somut Persistence'ta. Generic `IRepository<T>` kullanılmaz.
- **Unit of Work = `DbContext`**: handler tek `SaveChangesAsync` ile atomik yazar (örn. `USER_ACCOUNT` + `PATIENT`).
- Okuma tarafı repository kullanmaz.

## CQRS + Mediator
- **Command:** durum değiştirir, domain'den geçer ([[domain-modeli]]).
- **Query:** `AsNoTracking` projeksiyonla doğrudan DTO.
- Pipeline: loglama (dış) → doğrulama (iç) → handler. Transaction behavior yok: handler tek `SaveChangesAsync` ile atomik yazar. Tüm mesajlar `Result`/`Result<T>` döner; doğrulama hatası handler'a girmeden `Result` olarak döner. Endpoint iş kuralı içermez ([[api-uc-noktalari]]).

## State Pattern
`AppointmentState` taban; `Pending/Approved/Rejected/Cancelled/Completed` türevleri. Geçersiz geçiş `DomainException`. DB'de yalnız `status` string. Geçiş tablosu: [[randevu-kurallari]].

## Domain Events + Transactional Outbox
Çökme anında bildirimin kaybolmaması için: event handler **aynı transaction'da** `EMAIL_OUTBOX` satırı yazar; `BackgroundService` satırları `FOR UPDATE SKIP LOCKED` ile alıp MailKit ile gönderir, hatada üstel geri çekilmeyle yeniden dener. `dedup_key` benzersizliği tekrar gönderimi önler.

| Olay | Alıcı |
|---|---|
| `AppointmentRequestedEvent` | Hasta (alındı), Doktor (onay bekliyor) |
| `AppointmentApprovedEvent` | Hasta |
| `AppointmentRejectedEvent` | Hasta (red nedeni) |
| `AppointmentCancelledEvent` | Karşı taraf |

Tablo: [[veritabani]]. Test: [[test-stratejisi]].
