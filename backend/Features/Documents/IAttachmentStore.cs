using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.Documents;

public interface IAttachmentStore
{
    Task<Document?> FindDocumentAsync(Guid documentId, CancellationToken cancellationToken);
    Task<Document?> FindDocumentByNoAsync(
        Guid companyId, string documentNo, CancellationToken cancellationToken);
    Task<bool> AttachmentNoExistsAsync(
        Guid documentId, string attachmentNo, CancellationToken cancellationToken);
    Task<IReadOnlyList<Attachment>> ListAsync(
        Guid documentId, CancellationToken cancellationToken);
    Task<Attachment?> FindByIdAsync(Guid attachmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AttachmentVersion>> ListVersionsAsync(
        Guid attachmentId, CancellationToken cancellationToken);
    void Add(Attachment attachment);
    void Detach(Attachment attachment);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
