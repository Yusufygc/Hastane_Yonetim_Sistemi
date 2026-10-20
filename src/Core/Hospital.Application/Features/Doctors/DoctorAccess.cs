using Hospital.Application.Features.Departments;
using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Hospital.Domain.Entities.Facility;
using Hospital.Domain.Entities.Users;

namespace Hospital.Application.Features.Doctors;

/// <summary>Doktor ile bağlı olduğu şube (bölüm üzerinden).</summary>
internal sealed record DoctorContext(Doctor Doctor, HospitalBranch Hospital);

/// <summary>Doktor komutlarının ortak yükleme + yetki denetimi adımları.</summary>
internal static class DoctorAccess
{
    /// <summary>
    /// Yöneticinin kapsamındaki doktoru yükler. Kapsam dışı doktor "bulunamadı" gibi davranır (varlık sızmaz).
    /// </summary>
    public static async Task<Result<DoctorContext>> LoadForAdminAsync(
        Guid doctorId,
        DoctorInclude include,
        ICurrentUser currentUser,
        IDoctorRepository doctors,
        IDepartmentRepository departments,
        IHospitalRepository hospitals,
        CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is null)
        {
            return HospitalErrors.Forbidden;
        }

        var doctor = await doctors.GetByIdAsync(doctorId, include, cancellationToken);
        if (doctor is null)
        {
            return DoctorErrors.NotFound;
        }

        var hospital = await HospitalOfAsync(doctor, includeWorkingHours: false, departments, hospitals, cancellationToken);
        return hospital is null || !scope.CanAccessHospital(hospital.OrganizationId, hospital.Id)
            ? DoctorErrors.NotFound
            : new DoctorContext(doctor, hospital);
    }

    /// <summary>Oturum açmış doktorun kendisini yükler (kimlik belirteçten gelir).</summary>
    public static async Task<Result<DoctorContext>> LoadSelfAsync(
        DoctorInclude include,
        bool includeWorkingHours,
        ICurrentUser currentUser,
        IDoctorRepository doctors,
        IDepartmentRepository departments,
        IHospitalRepository hospitals,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return HospitalErrors.Forbidden;
        }

        var doctor = await doctors.GetByIdAsync(userId, include, cancellationToken);
        if (doctor is null)
        {
            return DoctorErrors.NotFound;
        }

        var hospital = await HospitalOfAsync(doctor, includeWorkingHours, departments, hospitals, cancellationToken);
        return hospital is null ? DoctorErrors.NotFound : new DoctorContext(doctor, hospital);
    }

    private static async Task<HospitalBranch?> HospitalOfAsync(
        Doctor doctor,
        bool includeWorkingHours,
        IDepartmentRepository departments,
        IHospitalRepository hospitals,
        CancellationToken cancellationToken)
    {
        var department = await departments.GetByIdAsync(doctor.DepartmentId, cancellationToken);
        return department is null
            ? null
            : await hospitals.GetByIdAsync(department.HospitalId, includeWorkingHours, cancellationToken);
    }
}
