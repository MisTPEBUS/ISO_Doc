using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.Auth;

public interface IAuthUserStore
{
    Task<User?> FindByEmpnoAsync(string empno, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<AuthOrgNames?> FindOrgNamesAsync(
        Guid companyId,
        Guid deptId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record AuthOrgNames(string CompanyName, string DeptName);
