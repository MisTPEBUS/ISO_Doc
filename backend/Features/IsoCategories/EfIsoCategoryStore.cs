using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.IsoCategories;

public sealed class EfIsoCategoryStore(IsoDbContext dbContext) : IIsoCategoryStore
{
    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.Companies.AnyAsync(company => company.Id == companyId, cancellationToken);

    public Task<bool> NameExistsAsync(
        Guid companyId,
        string name,
        Guid? excludingIsoCategoryId,
        CancellationToken cancellationToken) =>
        dbContext.IsoCategories.AnyAsync(
            category => category.CompanyId == companyId
                && category.Name == name
                && category.Id != excludingIsoCategoryId,
            cancellationToken);

    public Task<int> CountAsync(
        Guid? companyId,
        bool includeInactive,
        CancellationToken cancellationToken) =>
        Query(companyId, includeInactive).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<IsoCategory>> ListAsync(
        Guid? companyId,
        bool includeInactive,
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        await Query(companyId, includeInactive)
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<IsoCategory?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.IsoCategories.SingleOrDefaultAsync(category => category.Id == id, cancellationToken);

    public void Add(IsoCategory isoCategory) => dbContext.IsoCategories.Add(isoCategory);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<IsoCategory> Query(Guid? companyId, bool includeInactive)
    {
        var query = dbContext.IsoCategories.AsNoTracking();
        if (companyId.HasValue)
        {
            query = query.Where(category => category.CompanyId == companyId.Value);
        }

        if (!includeInactive)
        {
            query = query.Where(category => category.IsActive);
        }

        return query;
    }
}
