using System.Security.Claims;

namespace IsoDocument.Api.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    Guid? CompanyId { get; }

    Guid? DeptId { get; }

    UserRole? Role { get; }

    bool CanAccessCompany(Guid companyId);
}

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => ReadGuidClaim(ClaimTypes.NameIdentifier);

    public Guid? CompanyId => ReadGuidClaim(AuthClaimTypes.CompanyId);

    public Guid? DeptId => ReadGuidClaim(AuthClaimTypes.DeptId);

    public UserRole? Role => Enum.TryParse<UserRole>(
        Principal?.FindFirstValue(ClaimTypes.Role),
        ignoreCase: false,
        out var role)
        ? role
        : null;

    public bool CanAccessCompany(Guid companyId) =>
        CompanyAccessRules.CanAccess(Role, CompanyId, companyId);

    private Guid? ReadGuidClaim(string claimType) =>
        Guid.TryParse(Principal?.FindFirstValue(claimType), out var value)
            ? value
            : null;
}
