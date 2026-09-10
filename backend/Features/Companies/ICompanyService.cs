using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Companies.Dtos;

namespace IsoDocument.Api.Features.Companies;

public interface ICompanyService
{
    Task<Result<PagedResult<CompanyResponse>>> ListAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
