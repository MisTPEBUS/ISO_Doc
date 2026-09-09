using Microsoft.AspNetCore.Authorization;

namespace IsoDocument.Api.Security.Authorization;

public sealed class DocumentAccessRequirement : IAuthorizationRequirement;

public sealed record DocumentAccessResource(Guid DocumentId);
