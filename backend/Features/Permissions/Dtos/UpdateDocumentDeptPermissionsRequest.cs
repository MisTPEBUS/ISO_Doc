namespace IsoDocument.Api.Features.Permissions.Dtos;

public sealed record UpdateDocumentDeptPermissionsRequest(IReadOnlyList<Guid>? DeptIds);
