using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.Documents;

public sealed record AttachmentVersionContext(
    Document Document,
    DocumentVersion Version,
    string CompanyCode);

public sealed record AttachmentContext(
    Attachment Attachment,
    Guid CompanyId);

public sealed record AttachmentUploadContext(
    Attachment Attachment,
    Guid CompanyId,
    string CompanyCode,
    string DocumentNo,
    string Version,
    int Sequence);

public interface IAttachmentStore
{
    Task<AttachmentVersionContext?> FindVersionContextAsync(
        Guid documentId, Guid versionId, CancellationToken cancellationToken);
    Task<AttachmentContext?> FindAttachmentContextAsync(
        Guid attachmentId, CancellationToken cancellationToken);
    Task<AttachmentUploadContext?> FindAttachmentUploadContextAsync(
        Guid attachmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Attachment>> ListAsync(
        Guid versionId, CancellationToken cancellationToken);
    Task<IReadOnlySet<string>> FindExistingAttachmentNumbersAsync(
        Guid versionId, IReadOnlyCollection<string> attachmentNumbers,
        CancellationToken cancellationToken);
    void AddRange(IEnumerable<Attachment> attachments);
    void Remove(Attachment attachment);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<IAttachmentTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

public interface IAttachmentTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
