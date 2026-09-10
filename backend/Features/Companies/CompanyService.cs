using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Companies.Dtos;

namespace IsoDocument.Api.Features.Companies;

public sealed class CompanyService(ICompanyStore companyStore) : ICompanyService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    public async Task<Result<PagedResult<CompanyResponse>>> ListAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedKeyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        page = page > 0 ? page : DefaultPage;
        pageSize = pageSize > 0 ? Math.Min(pageSize, MaximumPageSize) : DefaultPageSize;

        var totalCount = await companyStore.CountAsync(normalizedKeyword, cancellationToken);
        var companies = await companyStore.ListAsync(
            normalizedKeyword,
            (page - 1) * pageSize,
            pageSize,
            cancellationToken);

        return Result<PagedResult<CompanyResponse>>.Success(new(
            companies.Select(ToResponse).ToArray(),
            page,
            pageSize,
            totalCount));
    }

    private static CompanyResponse ToResponse(Company company) =>
        new(company.Id, company.Code, company.Name);
}
