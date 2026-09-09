namespace IsoDocument.Api.Features.Permissions.Dtos;

public sealed record DocumentDeptPermissionsResponse(IReadOnlyList<Guid> DeptIds);
