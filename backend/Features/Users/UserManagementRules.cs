using IsoDocument.Api.Security;

namespace IsoDocument.Api.Features.Users;

public static class UserManagementRules
{
    public static bool CanCreateRole(UserRole? actorRole, UserRole requestedRole) =>
        actorRole is UserRole.SYSTEM_ADMIN
        || requestedRole is not UserRole.SYSTEM_ADMIN;

    public static bool CanUpdateRole(
        UserRole? actorRole,
        UserRole existingRole,
        UserRole requestedRole) =>
        actorRole is UserRole.SYSTEM_ADMIN
        || (existingRole is not UserRole.SYSTEM_ADMIN
            && requestedRole is not UserRole.SYSTEM_ADMIN);
}
