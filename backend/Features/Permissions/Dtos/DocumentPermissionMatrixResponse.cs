namespace IsoDocument.Api.Features.Permissions.Dtos;

public sealed record DocumentPermissionMatrixResponse(
    IReadOnlyList<DepartmentOptionResponse> Departments,
    IReadOnlyList<DocumentPermissionRowResponse> Items,
    PaginationResponse Pagination);

public sealed record DepartmentOptionResponse(
    Guid Id,
    string Name,
    int? Seq);

public sealed record DocumentPermissionRowResponse(
    Guid DocumentId,
    string DocumentCode,
    string DocumentName,
    string? Version,
    Guid CompanyId,
    DocumentStatusResponse Status,
    IReadOnlyCollection<Guid> DepartmentIds);

public sealed record DocumentStatusResponse(
    DocumentFileStatusResponse MainDocument,
    DocumentFileStatusResponse Attachment,
    DocumentEffectiveStatusResponse Effective);

public sealed record DocumentFileStatusResponse(
    string Code,
    string Label,
    bool HasError);

public sealed record DocumentEffectiveStatusResponse(
    string Code,
    string Label);

public sealed record PaginationResponse(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
