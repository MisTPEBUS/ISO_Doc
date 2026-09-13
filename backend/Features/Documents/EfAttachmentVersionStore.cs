using System.Data;
using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IsoDocument.Api.Features.Documents;

public sealed class EfAttachmentVersionStore(IsoDbContext dbContext) : IAttachmentVersionStore
{
    public Task<AttachmentVersionCreateContext?> FindCreateContextAsync(
        Guid attachmentId,
        CancellationToken cancellationToken) =>
        (from attachment in dbContext.Attachments
         join document in dbContext.Documents on attachment.DocumentId equals document.Id
         join company in dbContext.Companies on document.CompanyId equals company.Id
         where attachment.Id == attachmentId
         select new AttachmentVersionCreateContext(
             attachment.Id,
             attachment.AttachmentNo,
             attachment.IsActive,
             document.Id,
             document.CompanyId,
             document.DocumentNo,
             company.Code))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<AttachmentVersion?> FindLatestVersionAsync(
        Guid attachmentId,
        CancellationToken cancellationToken) =>
        dbContext.AttachmentVersions
            .Where(version => version.AttachmentId == attachmentId)
            .OrderByDescending(version => version.VersionMajor)
            .ThenByDescending(version => version.VersionMinor)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AttachmentVersion>> ListPublishedVersionsAsync(
        Guid attachmentId,
        CancellationToken cancellationToken) =>
        await dbContext.AttachmentVersions
            .Where(version => version.AttachmentId == attachmentId && version.Status == "PUBLISHED")
            .ToListAsync(cancellationToken);

    public Task<AttachmentVersion?> FindVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken) =>
        dbContext.AttachmentVersions.SingleOrDefaultAsync(
            version => version.Id == versionId, cancellationToken);

    public void Add(AttachmentVersion version) => dbContext.AttachmentVersions.Add(version);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IAttachmentVersionTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken) =>
        new EfAttachmentVersionTransaction(await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken));

    private sealed class EfAttachmentVersionTransaction(IDbContextTransaction transaction)
        : IAttachmentVersionTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
