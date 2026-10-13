using Hospital.Application.Wrappers;
using Mediator;

namespace Hospital.Application.Features.Catalog.Queries.GetCatalogDepartments;

public sealed class GetCatalogDepartmentsHandler(ICatalogReader reader)
    : IQueryHandler<GetCatalogDepartmentsQuery, Result<IReadOnlyList<CatalogDepartmentDto>>>
{
    public async ValueTask<Result<IReadOnlyList<CatalogDepartmentDto>>> Handle(
        GetCatalogDepartmentsQuery query,
        CancellationToken cancellationToken)
    {
        var departments = await reader.GetDepartmentsAsync(query.HospitalId, cancellationToken);
        return departments is null ? CatalogErrors.HospitalNotFound : Result.Success(departments);
    }
}
