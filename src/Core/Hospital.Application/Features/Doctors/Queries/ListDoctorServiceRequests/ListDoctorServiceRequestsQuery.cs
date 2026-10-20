using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Doctors.Queries.ListDoctorServiceRequests;

/// <param name="Status">PENDING (varsayılan), APPROVED veya REJECTED; büyük/küçük harf duyarsız.</param>
public sealed record ListDoctorServiceRequestsQuery(string? Status) : IQuery<Result<IReadOnlyList<DoctorServiceRequestDto>>>;
