namespace IsoDocument.Api.Security.Authorization;

public interface IDocumentAccessStore
{
    Task<Guid?> FindDocumentCompanyIdAsync(
        Guid documentId, CancellationToken cancellationToken);
    Task<Guid?> FindAttachmentDocumentIdAsync(
        Guid attachmentId, CancellationToken cancellationToken);
    Task<bool> DeptHasAccessAsync(
        Guid documentId, Guid deptId, CancellationToken cancellationToken);
}
