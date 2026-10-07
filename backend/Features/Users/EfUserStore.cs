using IsoDocument.Api.Common;
using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.Users;

public sealed class EfUserStore(IsoDbContext dbContext) : IUserStore
{
    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.Companies.AnyAsync(company => company.Id == companyId, cancellationToken);

    public Task<bool> DeptBelongsToCompanyAsync(
        Guid deptId,
        Guid companyId,
        CancellationToken cancellationToken) =>
        dbContext.Depts.AnyAsync(
            dept => dept.Id == deptId && dept.CompanyId == companyId,
            cancellationToken);

    public Task<bool> EmpnoExistsAsync(string empno, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.Empno == empno, cancellationToken);

    public Task<int> CountAsync(
        Guid? companyId,
        Guid? deptId,
        string? keyword,
        bool includeInactive,
        CancellationToken cancellationToken) =>
        Query(companyId, deptId, keyword, includeInactive).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<User>> ListAsync(
        Guid? companyId,
        Guid? deptId,
        string? keyword,
        bool includeInactive,
        ListSort? sort,
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        await ApplySort(Query(companyId, deptId, keyword, includeInactive), sort)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);

    public void Detach(User user) => dbContext.Entry(user).State = EntityState.Detached;

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<User> Query(
        Guid? companyId,
        Guid? deptId,
        string? keyword,
        bool includeInactive)
    {
        var query = dbContext.Users.AsNoTracking();
        if (companyId.HasValue)
        {
            query = query.Where(user => user.CompanyId == companyId.Value);
        }

        if (deptId.HasValue)
        {
            query = query.Where(user => user.DeptId == deptId.Value);
        }

        if (!includeInactive)
        {
            query = query.Where(user => user.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(user =>
                EF.Functions.ILike(user.Empno, pattern)
                || EF.Functions.ILike(user.Name, pattern)
                || (user.Email != null && EF.Functions.ILike(user.Email, pattern)));
        }

        return query;
    }

    private IOrderedQueryable<User> ApplySort(IQueryable<User> query, ListSort? sort)
    {
        var descending = sort?.Descending ?? false;
        var ordered = sort?.Field switch
        {
            UserSortFields.Name => query.OrderByDirection(user => user.Name, descending),
            UserSortFields.DeptName => query.OrderByDirection(
                user => dbContext.Depts
                    .Where(dept => dept.Id == user.DeptId)
                    .Select(dept => dept.Name)
                    .FirstOrDefault(),
                descending),
            UserSortFields.Email => query.OrderByNullsLast(user => user.Email, descending),
            UserSortFields.Role => query.OrderByDirection(user => user.Role, descending),
            UserSortFields.IsActive => query.OrderByDirection(user => user.IsActive, descending),
            UserSortFields.LastLoginAt => query.OrderByNullsLast(
                user => user.LastLoginAt, descending),
            _ => query.OrderByDirection(user => user.Empno, descending)
        };

        // 同值時依帳號、id 排列，確保分頁結果穩定。
        return ordered.ThenBy(user => user.Empno).ThenBy(user => user.Id);
    }
}
