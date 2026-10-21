using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Appointments;
using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Appointments;

/// <summary>Randevu komutlarının ortak yükleme + sahiplik denetimi adımları (başkasının randevusu → 404).</summary>
internal static class AppointmentAccess
{
    /// <summary>Yalnızca ilgili randevunun doktoru işlem yapabilir (onay, red, tamamlama).</summary>
    public static async Task<Result<Appointment>> LoadForDoctorAsync(
        Guid appointmentId,
        IAppointmentRepository appointments,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } doctorId)
        {
            return HospitalErrors.Forbidden;
        }

        var appointment = await appointments.GetByIdAsync(appointmentId, cancellationToken);
        return appointment is null || appointment.DoctorId != doctorId ? AppointmentErrors.NotFound : appointment;
    }

    /// <summary>
    /// Hasta kendi, doktor kendi randevusunu; yönetici kapsamındaki şubenin randevusunu görebilir/iptal edebilir.
    /// Kapsam/sahiplik dışı randevu "bulunamadı" gibi davranır (varlık sızmaz); personel rolü yetkisizdir.
    /// </summary>
    public static async Task<Error?> CheckViewAccessAsync(
        Guid patientId,
        Guid doctorId,
        Guid hospitalId,
        IHospitalRepository hospitals,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        switch (currentUser.Role)
        {
            case RoleNames.Patient:
                return currentUser.UserId == patientId ? null : AppointmentErrors.NotFound;

            case RoleNames.Doctor:
                return currentUser.UserId == doctorId ? null : AppointmentErrors.NotFound;

            case RoleNames.Admin:
                var scope = AdminScope.From(currentUser);
                var hospital = await hospitals.GetByIdAsync(hospitalId, includeWorkingHours: false, cancellationToken);
                return HospitalErrors.CheckAccess(scope, hospital) is null
                    ? null
                    : scope is null ? HospitalErrors.Forbidden : AppointmentErrors.NotFound;

            default:
                return HospitalErrors.Forbidden;
        }
    }
}
