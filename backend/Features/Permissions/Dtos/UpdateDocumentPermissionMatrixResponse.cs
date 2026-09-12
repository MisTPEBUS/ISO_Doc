namespace IsoDocument.Api.Features.Permissions.Dtos;

public sealed record UpdateDocumentPermissionMatrixResponse(
    IReadOnlyList<DocumentPermissionMatrixUpdateResult> Items,
    UpdatedByResponse UpdatedBy,
    DateTimeOffset UpdatedAt);

public sealed record DocumentPermissionMatrixUpdateResult(
    Guid DocumentId,
    string DocumentCode,
    IReadOnlyCollection<Guid> DepartmentIds,
    IReadOnlyCollection<Guid> AddedDepartmentIds,
    IReadOnlyCollection<Guid> RemovedDepartmentIds);

public sealed record UpdatedByResponse(Guid Id, string Name);
