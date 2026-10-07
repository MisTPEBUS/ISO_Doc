using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.Documents;

/// <summary>
/// 建立文件時預設全開：為公司底下全部部門各建一筆權限（SPEC 第 5 節「Permission」）。
/// 呼叫端須與文件寫入放在同一個 transaction。
/// </summary>
internal static class DefaultDocumentPermissions
{
    public static DocumentDeptPermission[] Create(
        Guid documentId,
        IEnumerable<Guid> deptIds,
        Guid grantedBy,
        DateTimeOffset now) =>
        deptIds
            .Select(deptId => new DocumentDeptPermission
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                DeptId = deptId,
                GrantedBy = grantedBy,
                CreatedAt = now
            })
            .ToArray();
}
