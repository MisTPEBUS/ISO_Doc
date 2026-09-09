namespace IsoDocument.Api.Security;

public static class CompanyAccessRules
{
    public static bool CanAccess(
        UserRole? role,
        Guid? currentCompanyId,
        Guid targetCompanyId) => role switch
    {
        UserRole.SYSTEM_ADMIN => true,
        UserRole.COMPANY_ADMIN => currentCompanyId == targetCompanyId,
        _ => false
    };

    public static CompanyFilterResolution ResolveCompanyFilter(
        UserRole? role,
        Guid? currentCompanyId,
        Guid? requestedCompanyId) => role switch
    {
        UserRole.SYSTEM_ADMIN => new(true, requestedCompanyId),
        UserRole.COMPANY_ADMIN when currentCompanyId.HasValue
            && (!requestedCompanyId.HasValue || requestedCompanyId == currentCompanyId) =>
            new(true, currentCompanyId),
        _ => new(false, null)
    };
}

public readonly record struct CompanyFilterResolution(bool IsAllowed, Guid? CompanyId);
