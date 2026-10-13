using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Services.Queries.GetService;

public sealed record GetServiceQuery(Guid Id) : IQuery<Result<ServiceDto>>;
