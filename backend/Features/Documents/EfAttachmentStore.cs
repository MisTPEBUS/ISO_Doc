using System.Data;
using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IsoDocument.Api.Features.Documents;

public sealed class EfAttachmentStore(IsoDbContext dbContext) : IAttachmentStore
{
    public Task<AttachmentVersionContext?> FindVersionContextAsync(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken) =>
        (from version in dbContext.DocumentVersions
         join document in dbContext.Documents on version.DocumentId equals document.Id
         join company in dbContext.Companies on document.CompanyId equals company.Id
         where version.Id == versionId && document.Id == documentId
         select new AttachmentVersionContext(document, version, company.Code))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<AttachmentContext?> FindAttachmentContextAsync(
        Guid attachmentId,
        CancellationToken cancellationToken) =>
        (from attachment in dbContext.Attachments
         join version in dbContext.DocumentVersions on attachment.DocumentVersionId equals version.Id
         join document in dbContext.Documents on version.DocumentId equals document.Id
         where attachment.Id == attachmentId
         select new AttachmentContext(attachment, document.CompanyId))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Attachment>> ListAsync(
        Guid versionId,
        CancellationToken cancellationToken) =>
        await dbContext.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.DocumentVersionId == versionId)
            .OrderBy(attachment => attachment.AttachmentNo)
            .ThenBy(attachment => attachment.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<string>> FindExistingAttachmentNumbersAsync(
        Guid versionId,
        IReadOnlyCollection<string> attachmentNumbers,
        CancellationToken cancellationToken) =>
        (await dbContext.Attachments
            .Where(attachment => attachment.DocumentVersionId == versionId
                && attachmentNumbers.Contains(attachment.AttachmentNo))
            .Select(attachment => attachment.AttachmentNo)
            .ToListAsync(cancellationToken))
        .ToHashSet(StringComparer.Ordinal);

    public void AddRange(IEnumerable<Attachment> attachments) =>
        dbContext.Attachments.AddRange(attachments);

    public void Remove(Attachment attachment) => dbContext.Attachments.Remove(attachment);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IAttachmentTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken) =>
        new EfAttachmentTransaction(await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken));

    private sealed class EfAttachmentTransaction(IDbContextTransaction transaction)
        : IAttachmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
