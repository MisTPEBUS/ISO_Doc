using System.Data;
using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IsoDocument.Api.Features.Documents;

public sealed class EfDocumentVersionStore(IsoDbContext dbContext) : IDocumentVersionStore
{
    public Task<Document?> FindDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        dbContext.Documents.SingleOrDefaultAsync(
            document => document.Id == documentId,
            cancellationToken);

    public Task<string?> FindCompanyCodeAsync(
        Guid companyId,
        CancellationToken cancellationToken) =>
        dbContext.Companies
            .Where(company => company.Id == companyId)
            .Select(company => company.Code)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<DocumentVersion?> FindVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken) =>
        dbContext.DocumentVersions.SingleOrDefaultAsync(
            version => version.Id == versionId,
            cancellationToken);

    public Task<DocumentVersion?> FindLatestVersionAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        dbContext.DocumentVersions
            .Where(version => version.DocumentId == documentId)
            .OrderByDescending(version => version.VersionMajor)
            .ThenByDescending(version => version.VersionMinor)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DocumentVersion>> ListPublishedVersionsAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        await dbContext.DocumentVersions
            .Where(version => version.DocumentId == documentId && version.Status == "PUBLISHED")
            .ToListAsync(cancellationToken);

    public void Add(DocumentVersion version) => dbContext.DocumentVersions.Add(version);

    public void Remove(DocumentVersion version) => dbContext.DocumentVersions.Remove(version);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IDocumentVersionTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken) =>
        new EfDocumentVersionTransaction(await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken));

    private sealed class EfDocumentVersionTransaction(IDbContextTransaction transaction)
        : IDocumentVersionTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
