using System.Data;
using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
            .Include(document => document.Dept)
            .OrderBy(document => document.DocumentNo)
            .ThenBy(document => document.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<Document?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Documents
            .Include(document => document.Dept)
            .SingleOrDefaultAsync(document => document.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DocumentVersion>> ListVersionsAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        await dbContext.DocumentVersions
            .AsNoTracking()
            .Where(version => version.DocumentId == documentId)
            .OrderByDescending(version => version.VersionMajor)
            .ThenByDescending(version => version.VersionMinor)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DocumentAttachmentRecord>> ListAttachmentsAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var attachments = await dbContext.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.DocumentId == documentId && attachment.IsActive)
            .OrderBy(attachment => attachment.AttachmentNo)
            .ThenBy(attachment => attachment.Id)
            .ToListAsync(cancellationToken);

        if (attachments.Count == 0)
        {
            return [];
        }

        var attachmentIds = attachments.Select(attachment => attachment.Id).ToArray();
        var representativeVersions = await dbContext.AttachmentVersions
            .AsNoTracking()
            .Where(version => attachmentIds.Contains(version.AttachmentId)
                && (version.Status == "PUBLISHED" || version.Status == "DRAFT"))
            .OrderBy(version => version.Status == "PUBLISHED" ? 0 : 1)
            .ThenByDescending(version => version.VersionMajor)
            .ThenByDescending(version => version.VersionMinor)
            .ToListAsync(cancellationToken);
        var currentVersions = representativeVersions
            .GroupBy(version => version.AttachmentId)
            .ToDictionary(group => group.Key, group => group.First());

        return attachments
            .Select(attachment => new DocumentAttachmentRecord(
                attachment,
                currentVersions.GetValueOrDefault(attachment.Id)))
            .ToArray();
    }

    public async Task<IReadOnlyList<Guid>> ListCompanyDeptIdsAsync(
        Guid companyId,
        CancellationToken cancellationToken) =>
        await dbContext.Depts
            .Where(dept => dept.CompanyId == companyId)
            .OrderBy(dept => dept.Id)
            .Select(dept => dept.Id)
            .ToListAsync(cancellationToken);

    public Task<bool> IsoCategoryBelongsToCompanyAsync(
        Guid companyId,
        Guid isoCategoryId,
        CancellationToken cancellationToken) =>
        dbContext.IsoCategories.AnyAsync(
            category => category.Id == isoCategoryId && category.CompanyId == companyId,
            cancellationToken);

    public Task<Dept?> FindCompanyDeptAsync(
        Guid companyId,
        Guid deptId,
        CancellationToken cancellationToken) =>
        dbContext.Depts.SingleOrDefaultAsync(
            dept => dept.Id == deptId && dept.CompanyId == companyId,
            cancellationToken);

    public void Add(Document document) => dbContext.Documents.Add(document);

    public void Add(DocumentVersion version) => dbContext.DocumentVersions.Add(version);

    public void AddRange(IEnumerable<DocumentDeptPermission> permissions) =>
        dbContext.DocumentDeptPermissions.AddRange(permissions);

    public void Detach(Document document) =>
        dbContext.Entry(document).State = EntityState.Detached;

    public void Detach(DocumentVersion version) =>
        dbContext.Entry(version).State = EntityState.Detached;

    public void DetachRange(IEnumerable<DocumentDeptPermission> permissions)
    {
        foreach (var permission in permissions)
        {
            dbContext.Entry(permission).State = EntityState.Detached;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IDocumentTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken) =>
        new EfDocumentTransaction(await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken));

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

    private sealed class EfDocumentTransaction(IDbContextTransaction transaction)
        : IDocumentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
