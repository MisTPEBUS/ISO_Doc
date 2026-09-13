using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.Documents;

public sealed class EfAttachmentStore(IsoDbContext dbContext) : IAttachmentStore
{
    public Task<Document?> FindDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        dbContext.Documents.SingleOrDefaultAsync(
            document => document.Id == documentId, cancellationToken);

    public Task<Document?> FindDocumentByNoAsync(
        Guid companyId,
        string documentNo,
        CancellationToken cancellationToken) =>
        dbContext.Documents.SingleOrDefaultAsync(
            document => document.CompanyId == companyId
                && document.DocumentNo == documentNo,
            cancellationToken);

    public Task<bool> AttachmentNoExistsAsync(
        Guid documentId,
        string attachmentNo,
        CancellationToken cancellationToken) =>
        dbContext.Attachments.AnyAsync(
            attachment => attachment.DocumentId == documentId
                && attachment.AttachmentNo == attachmentNo,
            cancellationToken);

    public async Task<IReadOnlyList<Attachment>> ListAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        await dbContext.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.DocumentId == documentId && attachment.IsActive)
            .OrderBy(attachment => attachment.AttachmentNo)
            .ThenBy(attachment => attachment.Id)
            .ToListAsync(cancellationToken);

    public Task<Attachment?> FindByIdAsync(
        Guid attachmentId,
        CancellationToken cancellationToken) =>
        dbContext.Attachments.SingleOrDefaultAsync(
            attachment => attachment.Id == attachmentId, cancellationToken);

    public async Task<IReadOnlyList<AttachmentVersion>> ListVersionsAsync(
        Guid attachmentId,
        CancellationToken cancellationToken) =>
        await dbContext.AttachmentVersions
            .AsNoTracking()
            .Where(version => version.AttachmentId == attachmentId)
            .OrderByDescending(version => version.VersionMajor)
            .ThenByDescending(version => version.VersionMinor)
            .ToListAsync(cancellationToken);

    public void Add(Attachment attachment) => dbContext.Attachments.Add(attachment);

    public void Detach(Attachment attachment) =>
        dbContext.Entry(attachment).State = EntityState.Detached;

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
