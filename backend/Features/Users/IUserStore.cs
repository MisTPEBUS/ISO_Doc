using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.Users;

public interface IUserStore
{
    Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken);
    Task<bool> DeptBelongsToCompanyAsync(Guid deptId, Guid companyId, CancellationToken cancellationToken);
    Task<bool> EmpnoExistsAsync(string empno, CancellationToken cancellationToken);
    Task<int> CountAsync(
        Guid? companyId,
        Guid? deptId,
        string? keyword,
        bool includeInactive,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> ListAsync(
        Guid? companyId,
        Guid? deptId,
        string? keyword,
        bool includeInactive,
        int skip,
        int take,
        CancellationToken cancellationToken);
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    void Add(User user);
    void Detach(User user);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
