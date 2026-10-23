using System.Globalization;
using Hospital.Domain.Entities.Notifications;

namespace Hospital.Application.Features.Notifications;

/// <summary>Hazırlanmış e-posta içeriği. <c>TemplateKey</c> outbox satırında saklanır (izleme için).</summary>
public sealed record EmailContent(string TemplateKey, string Subject, string Body);

/// <summary>
/// Randevu e-posta şablonları (Türkçe, düz metin). Tarih/saat şubenin saat diliminde yazılır; hasta notu gibi serbest
/// metinler e-postaya konmaz (yalnız doktor/hasta adı, hizmet, şube, zaman ve varsa red/iptal nedeni).
/// </summary>
public static class AppointmentEmails
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string FormatLocal(DateTimeOffset utc, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return TimeZoneInfo.ConvertTime(utc, zone).ToString("dd.MM.yyyy HH:mm", Turkish);
    }

    public static EmailContent RequestedForPatient(AppointmentRecipients r, string service, DateTimeOffset startAt) =>
        new(
            "appointment.requested.patient",
            "Randevu talebiniz alındı",
            $"""
             Merhaba {r.PatientName},

             {service} randevu talebiniz alındı. Doktorun onayı bekleniyor; karar verildiğinde size e-posta ile bildireceğiz.

             {Details(r, service, startAt)}
             """);

    public static EmailContent RequestedForDoctor(AppointmentRecipients r, string service, DateTimeOffset startAt) =>
        new(
            "appointment.requested.doctor",
            "Yeni randevu talebi onayınızı bekliyor",
            $"""
             Merhaba {r.DoctorName},

             {r.PatientName} adlı hasta {service} için randevu talep etti. Lütfen sistemden onaylayın veya reddedin.

             {Details(r, service, startAt)}
             """);

    public static EmailContent Approved(AppointmentRecipients r, string service, DateTimeOffset startAt) =>
        new(
            "appointment.approved.patient",
            "Randevunuz onaylandı",
            $"""
             Merhaba {r.PatientName},

             {service} randevunuz doktor tarafından onaylandı.

             {Details(r, service, startAt)}
             """);

    public static EmailContent Rejected(AppointmentRecipients r, string service, DateTimeOffset startAt, string reason) =>
        new(
            "appointment.rejected.patient",
            "Randevu talebiniz onaylanmadı",
            $"""
             Merhaba {r.PatientName},

             Üzgünüz, {service} randevu talebiniz onaylanmadı.
             Neden: {reason}

             {Details(r, service, startAt)}

             Sistemden uygun başka bir saat için yeniden randevu talep edebilirsiniz.
             """);

    public static EmailContent CancelledForPatient(AppointmentRecipients r, string service, DateTimeOffset startAt, string reason, string cancelledBy) =>
        new(
            "appointment.cancelled.patient",
            "Randevunuz iptal edildi",
            $"""
             Merhaba {r.PatientName},

             {service} randevunuz {cancelledBy} tarafından iptal edildi.
             Neden: {reason}

             {Details(r, service, startAt)}
             """);

    public static EmailContent CancelledForDoctor(AppointmentRecipients r, string service, DateTimeOffset startAt, string reason, string cancelledBy) =>
        new(
            "appointment.cancelled.doctor",
            "Bir randevu iptal edildi",
            $"""
             Merhaba {r.DoctorName},

             {r.PatientName} adlı hastanın {service} randevusu {cancelledBy} tarafından iptal edildi.
             Neden: {reason}

             {Details(r, service, startAt)}
             """);

    private static string Details(AppointmentRecipients r, string service, DateTimeOffset startAt) =>
        $"""
         Hizmet: {service}
         Doktor: {r.DoctorName}
         Şube: {r.HospitalName}
         Tarih / saat: {FormatLocal(startAt, r.TimeZoneId)} ({r.TimeZoneId})
         """;

    /// <summary>Olay kimliği + alıcı anahtarı: aynı olay için aynı alıcıya ikinci e-posta eklenmez.</summary>
    public static string DedupKey(Guid eventId, string recipientKey) => $"{eventId:N}:{recipientKey}";

    public static EmailOutboxMessage ToMessage(
        Guid eventId,
        string recipientKey,
        string toAddress,
        string toName,
        EmailContent content,
        DateTimeOffset now) =>
        EmailOutboxMessage.Create(DedupKey(eventId, recipientKey), toAddress, toName, content.Subject, content.Body, content.TemplateKey, now);
}
