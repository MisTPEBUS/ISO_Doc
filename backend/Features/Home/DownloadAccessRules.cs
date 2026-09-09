using IsoDocument.Api.Security;

namespace IsoDocument.Api.Features.Home;

public static class DownloadAccessRules
{
    public static bool CanDownload(UserRole? role, string versionStatus) => role switch
    {
        UserRole.USER => versionStatus == "PUBLISHED",
        UserRole.COMPANY_ADMIN or UserRole.SYSTEM_ADMIN =>
            versionStatus is "PUBLISHED" or "OBSOLETE",
        _ => false
    };
}
