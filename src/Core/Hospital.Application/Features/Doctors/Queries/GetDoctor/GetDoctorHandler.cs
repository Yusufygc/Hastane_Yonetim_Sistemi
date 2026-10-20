using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.GetDoctor;

public sealed class GetDoctorHandler(IDoctorReader reader, ICurrentUser currentUser)
    : IQueryHandler<GetDoctorQuery, Result<DoctorDetailDto>>
{
    public async ValueTask<Result<DoctorDetailDto>> Handle(GetDoctorQuery query, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is null)
        {
            return HospitalErrors.Forbidden;
        }

        // Kapsam dışı doktor reader'da süzülür → "bulunamadı".
        var doctor = await reader.GetAsync(query.Id, scope, cancellationToken);
        return doctor is null ? DoctorErrors.NotFound : doctor;
    }
}
