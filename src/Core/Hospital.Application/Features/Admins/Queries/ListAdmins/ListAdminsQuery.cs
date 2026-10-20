using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Admins.Queries.ListAdmins;

public sealed record ListAdminsQuery : IQuery<Result<IReadOnlyList<AdminDto>>>;
