using IsoDocument.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.AiImport;

public sealed class EfAiImportStore(IsoDbContext dbContext) : IAiImportStore
{
    public async Task<DocumentImportState?> FindDocumentStateAsync(
        Guid companyId, string documentNo, CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents.SingleOrDefaultAsync(
            candidate => candidate.CompanyId == companyId && candidate.DocumentNo == documentNo,
            cancellationToken);
        if (document is null)
        {
            return null;
        }

        var latestVersion = await dbContext.DocumentVersions
            .Where(version => version.DocumentId == document.Id)
            .OrderByDescending(version => version.VersionMajor)
            .ThenByDescending(version => version.VersionMinor)
            .FirstOrDefaultAsync(cancellationToken);

        return new DocumentImportState(document, latestVersion);
    }

    public async Task<AttachmentImportState?> FindAttachmentStateAsync(
        Guid documentId, string attachmentNo, CancellationToken cancellationToken)
    {
        var attachment = await dbContext.Attachments.SingleOrDefaultAsync(
            candidate => candidate.DocumentId == documentId && candidate.AttachmentNo == attachmentNo,
            cancellationToken);
        if (attachment is null)
        {
            return null;
        }

        var latestVersion = await dbContext.AttachmentVersions
            .Where(version => version.AttachmentId == attachment.Id)
            .OrderByDescending(version => version.VersionMajor)
            .ThenByDescending(version => version.VersionMinor)
            .FirstOrDefaultAsync(cancellationToken);

        return new AttachmentImportState(attachment, latestVersion);
    }
}
