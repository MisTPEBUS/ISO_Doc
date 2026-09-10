using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.Auth;

public sealed class EfAuthUserStore(IsoDbContext dbContext) : IAuthUserStore
{
    public Task<User?> FindByEmpnoAsync(string empno, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Empno == empno, cancellationToken);

    public Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<AuthOrgNames?> FindOrgNamesAsync(
        Guid companyId,
        Guid deptId,
        CancellationToken cancellationToken) =>
        (from company in dbContext.Companies
         from dept in dbContext.Depts
         where company.Id == companyId && dept.Id == deptId
         select new AuthOrgNames(company.Name, dept.Name))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
