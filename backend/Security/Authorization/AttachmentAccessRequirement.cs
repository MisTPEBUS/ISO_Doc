using Microsoft.AspNetCore.Authorization;

namespace IsoDocument.Api.Security.Authorization;

public sealed class AttachmentAccessRequirement : IAuthorizationRequirement;

public sealed record AttachmentAccessResource(Guid AttachmentId);
