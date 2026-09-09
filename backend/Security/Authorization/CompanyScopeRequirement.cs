using Microsoft.AspNetCore.Authorization;

namespace IsoDocument.Api.Security.Authorization;

public sealed class CompanyScopeRequirement : IAuthorizationRequirement;

public sealed record CompanyScopeResource(Guid TargetCompanyId);
