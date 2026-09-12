namespace IsoDocument.Api.Features.Permissions.Dtos;

public sealed class UpdateDocumentPermissionMatrixRequest
{
    public IReadOnlyList<UpdateDocumentPermissionMatrixItemRequest> Items { get; init; } = [];
}

public sealed class UpdateDocumentPermissionMatrixItemRequest
{
    public Guid DocumentId { get; init; }

    /// <summary>
    /// 這份文件目前應有的完整部門清單（整批覆蓋，非 diff）。
    /// 可以是空陣列，代表清空這份文件的所有部門權限；不可為 null。
    /// </summary>
    public IReadOnlyList<Guid> DepartmentIds { get; init; } = [];
}
