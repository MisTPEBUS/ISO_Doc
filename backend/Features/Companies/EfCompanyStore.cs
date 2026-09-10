using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.Companies;

public sealed class EfCompanyStore(IsoDbContext dbContext) : ICompanyStore
{
    public Task<int> CountAsync(string? keyword, CancellationToken cancellationToken) =>
        Query(keyword).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Company>> ListAsync(
        string? keyword,
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        await Query(keyword)
            .OrderBy(company => company.Code)
            .ThenBy(company => company.Name)
            .ThenBy(company => company.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    private IQueryable<Company> Query(string? keyword)
    {
        var query = dbContext.Companies.AsNoTracking();
        return string.IsNullOrEmpty(keyword)
            ? query
            : query.Where(company =>
                EF.Functions.ILike(company.Code, $"%{keyword}%")
                || EF.Functions.ILike(company.Name, $"%{keyword}%"));
    }
}
