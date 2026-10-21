using Hospital.Application.Features.Appointments;
using Hospital.Application.Security;
using Hospital.Domain.Entities.Appointments;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Hospital.Persistence.Features.Appointments;

internal sealed class AppointmentReader(AppDbContext db) : IAppointmentReader
{
    public async Task<AppointmentDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await Rows().Where(r => r.Id == id).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToDto(row);
    }

    public async Task<IReadOnlyList<AppointmentDto>> ListForPatientAsync(
        Guid patientId,
        AppointmentStatus? status,
        CancellationToken cancellationToken) =>
        await ToListAsync(
            Rows().Where(r => r.PatientId == patientId && (status == null || r.Status == status))
                .OrderByDescending(r => r.StartAt),
            cancellationToken);

    public async Task<IReadOnlyList<AppointmentDto>> ListForDoctorAsync(
        Guid doctorId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        AppointmentStatus? status,
        CancellationToken cancellationToken) =>
        await ToListAsync(
            Rows().Where(r => r.DoctorId == doctorId
                              && (status == null || r.Status == status)
                              && (from == null || r.StartAt >= from)
                              && (to == null || r.StartAt <= to))
                .OrderBy(r => r.StartAt),
            cancellationToken);

    public async Task<IReadOnlyList<AppointmentDto>> ListForAdminAsync(
        AdminScope scope,
        Guid? hospitalId,
        AppointmentStatus? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken) =>
        await ToListAsync(
            Rows().Where(r => r.OrganizationId == scope.OrganizationId
                              && (scope.HospitalId == null || r.HospitalId == scope.HospitalId)
                              && (hospitalId == null || r.HospitalId == hospitalId)
                              && (status == null || r.Status == status)
                              && (from == null || r.StartAt >= from)
                              && (to == null || r.StartAt <= to))
                .OrderByDescending(r => r.StartAt),
            cancellationToken);

    private static async Task<IReadOnlyList<AppointmentDto>> ToListAsync(IQueryable<Row> query, CancellationToken cancellationToken)
    {
        var rows = await query.Take(AppointmentReaderLimits.MaxItems).ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    /// <summary>Randevu + şube + hasta + doktor birleşiminin düz satırı (hizmet adı/ücret snapshot'tan okunur).</summary>
    private sealed class Row
    {
        public Guid Id { get; init; }
        public Guid HospitalId { get; init; }
        public Guid OrganizationId { get; init; }
        public string HospitalName { get; init; } = null!;
        public Guid PatientId { get; init; }
        public string PatientName { get; init; } = null!;
        public Guid DoctorId { get; init; }
        public string DoctorName { get; init; } = null!;
        public Guid ServiceId { get; init; }
        public string ServiceName { get; init; } = null!;
        public int DurationMinutes { get; init; }
        public decimal Price { get; init; }
        public string Currency { get; init; } = null!;
        public DateTimeOffset StartAt { get; init; }
        public DateTimeOffset EndAt { get; init; }
        public AppointmentStatus Status { get; init; }
        public string? PatientNote { get; init; }
        public string? DecisionReason { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    private IQueryable<Row> Rows() =>
        from a in db.Appointments.AsNoTracking()
        join h in db.Hospitals on a.HospitalId equals h.Id
        join p in db.Patients on a.PatientId equals p.Id
        join d in db.Doctors on a.DoctorId equals d.Id
        select new Row
        {
            Id = a.Id,
            HospitalId = a.HospitalId,
            OrganizationId = h.OrganizationId,
            HospitalName = h.Name,
            PatientId = a.PatientId,
            PatientName = p.FirstName + " " + p.LastName,
            DoctorId = a.DoctorId,
            DoctorName = d.FirstName + " " + d.LastName,
            ServiceId = a.ServiceId,
            ServiceName = a.ServiceNameSnapshot,
            DurationMinutes = a.DurationMinutesSnapshot,
            Price = a.PriceSnapshot,
            Currency = a.CurrencySnapshot,
            StartAt = a.StartAt,
            EndAt = a.EndAt,
            Status = a.Status,
            PatientNote = a.PatientNote,
            DecisionReason = a.DecisionReason,
            CreatedAt = a.CreatedAt,
        };

    private static AppointmentDto ToDto(Row r) =>
        new(
            r.Id,
            r.HospitalId,
            r.HospitalName,
            r.PatientId,
            r.PatientName,
            r.DoctorId,
            r.DoctorName,
            r.ServiceId,
            r.ServiceName,
            r.DurationMinutes,
            r.Price,
            r.Currency,
            r.StartAt,
            r.EndAt,
            AppointmentStatusNames.From(r.Status),
            r.PatientNote,
            r.DecisionReason,
            r.CreatedAt);
}
