using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.Depts;

public sealed class EfDeptStore(IsoDbContext dbContext) : IDeptStore
{
    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.Companies.AnyAsync(company => company.Id == companyId, cancellationToken);

    public Task<bool> NameExistsAsync(
        Guid companyId,
        string name,
        Guid? excludingDeptId,
        CancellationToken cancellationToken) =>
        dbContext.Depts.AnyAsync(
            dept => dept.CompanyId == companyId
                && dept.Name == name
                && (!excludingDeptId.HasValue || dept.Id != excludingDeptId.Value),
            cancellationToken);

    public Task<int> CountAsync(Guid? companyId, CancellationToken cancellationToken) =>
        Query(companyId).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Dept>> ListAsync(
        Guid? companyId,
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        await Query(companyId)
            .OrderBy(dept => dept.Seq == null)
            .ThenBy(dept => dept.Seq)
            .ThenBy(dept => dept.Name)
            .ThenBy(dept => dept.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<Dept?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Depts.SingleOrDefaultAsync(dept => dept.Id == id, cancellationToken);

    public Task<bool> HasActiveUsersAsync(Guid deptId, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(
            user => user.DeptId == deptId && user.IsActive,
            cancellationToken);

    public void Add(Dept dept) => dbContext.Depts.Add(dept);

    public void Remove(Dept dept) => dbContext.Depts.Remove(dept);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<Dept> Query(Guid? companyId)
    {
        var query = dbContext.Depts.AsNoTracking();
        return companyId.HasValue
            ? query.Where(dept => dept.CompanyId == companyId.Value)
            : query;
    }
}
