namespace IsoDocument.Api.Data.Entities;

public sealed class DocumentDeptPermission
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DeptId { get; set; }
    public Guid GrantedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
