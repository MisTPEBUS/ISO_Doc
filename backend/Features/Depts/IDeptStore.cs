using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.Depts;

public interface IDeptStore
{
    Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(
        Guid companyId,
        string name,
        Guid? excludingDeptId,
        CancellationToken cancellationToken);

    Task<int> CountAsync(Guid? companyId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Dept>> ListAsync(
        Guid? companyId,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<Dept?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> HasActiveUsersAsync(Guid deptId, CancellationToken cancellationToken);

    void Add(Dept dept);

    void Remove(Dept dept);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
