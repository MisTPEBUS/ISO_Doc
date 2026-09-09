using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.Auth;

public interface IAuthUserStore
{
    Task<User?> FindByEmpnoAsync(string empno, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
