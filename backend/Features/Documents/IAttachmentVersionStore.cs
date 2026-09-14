using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.Documents;

public sealed record AttachmentVersionCreateContext(
    Guid AttachmentId,
    string AttachmentNo,
    bool AttachmentIsActive,
    Guid DocumentId,
    Guid CompanyId,
    string DocumentNo,
    string CompanyCode);

public interface IAttachmentVersionStore
{
    Task<AttachmentVersionCreateContext?> FindCreateContextAsync(
        Guid attachmentId, CancellationToken cancellationToken);
    Task<AttachmentVersion?> FindLatestVersionAsync(
        Guid attachmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AttachmentVersion>> ListPublishedVersionsAsync(
        Guid attachmentId, CancellationToken cancellationToken);
    Task<AttachmentVersion?> FindVersionAsync(
        Guid versionId, CancellationToken cancellationToken);
    void Add(AttachmentVersion version);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<IAttachmentVersionTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

public interface IAttachmentVersionTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
