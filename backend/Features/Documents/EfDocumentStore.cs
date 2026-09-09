using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.Documents;

public sealed class EfDocumentStore(IsoDbContext dbContext) : IDocumentStore
{
    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.Companies.AnyAsync(company => company.Id == companyId, cancellationToken);

    public Task<bool> DocumentNoExistsAsync(
        Guid companyId,
        string documentNo,
        CancellationToken cancellationToken) =>
        dbContext.Documents.AnyAsync(
            document => document.CompanyId == companyId
                && document.DocumentNo == documentNo,
            cancellationToken);

    public Task<int> CountAsync(
        Guid? companyId,
        string? keyword,
        CancellationToken cancellationToken) =>
        Query(companyId, keyword).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Document>> ListAsync(
        Guid? companyId,
        string? keyword,
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        await Query(companyId, keyword)
            .OrderBy(document => document.DocumentNo)
            .ThenBy(document => document.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<Document?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Documents.SingleOrDefaultAsync(document => document.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DocumentVersion>> ListVersionsAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        await dbContext.DocumentVersions
            .AsNoTracking()
            .Where(version => version.DocumentId == documentId)
            .OrderByDescending(version => version.VersionMajor)
            .ThenByDescending(version => version.VersionMinor)
            .ToListAsync(cancellationToken);

    public void Add(Document document) => dbContext.Documents.Add(document);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<Document> Query(Guid? companyId, string? keyword)
    {
        var query = dbContext.Documents.AsNoTracking();
        if (companyId.HasValue)
        {
            query = query.Where(document => document.CompanyId == companyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(document =>
                EF.Functions.ILike(document.DocumentNo, pattern)
                || EF.Functions.ILike(document.Name, pattern));
        }

        return query;
    }
}
