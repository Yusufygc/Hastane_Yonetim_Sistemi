using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Doctors;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Features.Services;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Facility;
using Hospital.Domain.Entities.Users;
using Hospital.Domain.Scheduling;

namespace Hospital.Application.Features.Appointments;

/// <summary>Randevu ön koşulları doğrulanmış doktor + şube + hizmet üçlüsü.</summary>
public sealed record SchedulingContext(Doctor Doctor, HospitalBranch Hospital, Service Service, TimeZoneInfo TimeZone)
{
    /// <summary>Şubenin yerel takvim günü.</summary>
    public DateOnly LocalDateOf(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime);
}

/// <summary>
/// Slot sorgusu ve randevu oluşturma aynı kuralları paylaşır (tek kaynak): doktor/şube/hizmet tutarlılığı, hizmetin
/// <b>onaylı</b> sunulması ve <see cref="SlotCalculator"/> ile üretilen müsait saatler.
/// </summary>
public sealed class SlotAvailabilityService(
    IDoctorRepository doctors,
    IDepartmentRepository departments,
    IHospitalRepository hospitals,
    IServiceRepository services,
    IAppointmentRepository appointments,
    AppointmentSettings settings,
    TimeProvider clock)
{
    public async Task<Result<SchedulingContext>> LoadAsync(Guid doctorId, Guid serviceId, CancellationToken cancellationToken)
    {
        var doctor = await doctors.GetForSchedulingAsync(doctorId, cancellationToken);
        if (doctor is not { IsActive: true })
        {
            return AppointmentErrors.DoctorNotFound;
        }

        var department = await departments.GetByIdAsync(doctor.DepartmentId, cancellationToken);
        var hospital = department is null
            ? null
            : await hospitals.GetByIdAsync(department.HospitalId, includeWorkingHours: true, cancellationToken);

        if (hospital is not { IsActive: true })
        {
            return AppointmentErrors.DoctorNotFound;
        }

        var service = await services.GetByIdAsync(serviceId, cancellationToken);
        if (service is not { IsActive: true } || service.HospitalId != hospital.Id)
        {
            return AppointmentErrors.ServiceInvalid;
        }

        if (doctor.FindService(service.Id)?.Status != DoctorServiceStatus.Approved)
        {
            return AppointmentErrors.ServiceNotOffered;
        }

        return new SchedulingContext(doctor, hospital, service, TimeZoneInfo.FindSystemTimeZoneById(hospital.TimeZoneId));
    }

    /// <summary>Şubenin yerel <paramref name="date"/> günü için müsait slotlar (UTC, başlangıca göre sıralı).</summary>
    public async Task<IReadOnlyList<TimeRange>> ComputeSlotsAsync(SchedulingContext context, DateOnly date, CancellationToken cancellationToken)
    {
        var bounds = SlotCalculator.LocalDayBounds(date, context.TimeZone);
        var booked = await appointments.ListBookedRangesAsync(context.Doctor.Id, bounds, cancellationToken);

        return SlotCalculator.Calculate(new SlotParameters(
            date,
            context.TimeZone,
            context.Service.DurationMinutes,
            settings.SlotGridMinutes,
            clock.GetUtcNow(),
            settings.MinLead,
            settings.MaxBookingHorizonDays,
            context.Hospital.WorkingHours,
            context.Doctor.Availabilities,
            [.. context.Doctor.TimeOffs.Select(t => new TimeRange(t.StartAt, t.EndAt))],
            booked));
    }
}
