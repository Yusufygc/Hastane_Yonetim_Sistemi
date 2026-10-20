using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.ListDoctors;

public sealed class ListDoctorsHandler(
    IHospitalRepository hospitals,
    IDoctorReader reader,
    ICurrentUser currentUser) : IQueryHandler<ListDoctorsQuery, Result<IReadOnlyList<DoctorDto>>>
{
    public async ValueTask<Result<IReadOnlyList<DoctorDto>>> Handle(ListDoctorsQuery query, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is null)
        {
            return HospitalErrors.Forbidden;
        }

        if (query.HospitalId is { } hospitalId)
        {
            var hospital = await hospitals.GetByIdAsync(hospitalId, includeWorkingHours: false, cancellationToken);
            if (HospitalErrors.CheckAccess(scope, hospital) is { } error)
            {
                return error;
            }
        }

        return Result.Success(await reader.ListAsync(scope, query.HospitalId, query.DepartmentId, cancellationToken));
    }
}
