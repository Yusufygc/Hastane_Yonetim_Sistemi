using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.ListDoctors;

/// <param name="HospitalId">Verilirse yalnızca o şubenin doktorları.</param>
/// <param name="DepartmentId">Verilirse yalnızca o bölümün doktorları.</param>
public sealed record ListDoctorsQuery(Guid? HospitalId, Guid? DepartmentId) : IQuery<Result<IReadOnlyList<DoctorDto>>>;
