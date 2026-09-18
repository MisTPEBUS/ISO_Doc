using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.AiImport;

public sealed record DocumentImportState(Document Document, DocumentVersion? LatestVersion);

public sealed record AttachmentImportState(Attachment Attachment, AttachmentVersion? LatestVersion);

public interface IAiImportStore
{
    Task<DocumentImportState?> FindDocumentStateAsync(
        Guid companyId, string documentNo, CancellationToken cancellationToken);

    Task<AttachmentImportState?> FindAttachmentStateAsync(
        Guid documentId, string attachmentNo, CancellationToken cancellationToken);
}
