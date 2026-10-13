using Hospital.Application.Features.Departments;

namespace Hospital.Application.Features.Services;

/// <summary>Hizmete bağlanacak bölümün aynı şubeye ait ve aktif olduğunu doğrular (Create/Update ortak).</summary>
internal static class ServiceDepartmentCheck
{
    public static async Task<bool> IsValidAsync(
        IDepartmentRepository departments,
        Guid hospitalId,
        Guid? departmentId,
        CancellationToken cancellationToken)
    {
        if (departmentId is null)
        {
            return true;
        }

        var department = await departments.GetByIdAsync(departmentId.Value, cancellationToken);
        return department is { IsActive: true } && department.HospitalId == hospitalId;
    }
}
