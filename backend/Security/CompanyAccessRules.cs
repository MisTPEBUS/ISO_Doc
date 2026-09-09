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
}
