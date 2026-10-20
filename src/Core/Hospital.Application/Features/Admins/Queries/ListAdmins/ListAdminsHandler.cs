using Hospital.Application.Features.Hospitals;
using Hospital.Application.Interfaces;
using Hospital.Application.Security;
using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Admins.Queries.ListAdmins;

public sealed class ListAdminsHandler(IAdminReader reader, ICurrentUser currentUser)
    : IQueryHandler<ListAdminsQuery, Result<IReadOnlyList<AdminDto>>>
{
    public async ValueTask<Result<IReadOnlyList<AdminDto>>> Handle(ListAdminsQuery query, CancellationToken cancellationToken)
    {
        var scope = AdminScope.From(currentUser);
        if (scope is null)
        {
            return HospitalErrors.Forbidden;
        }

        return Result.Success(await reader.ListAsync(scope, cancellationToken));
    }
}
