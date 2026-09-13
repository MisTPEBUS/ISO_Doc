using IsoDocument.Api.Data;
using IsoDocument.Api.Features.Home.Dtos;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.Home;

public sealed class EfDocumentsBrowseStore(IsoDbContext dbContext) : IDocumentsBrowseStore
{
    public Task<int> CountAvailableAsync(
        Guid deptId,
        string? keyword,
        CancellationToken cancellationToken)
    {
        return AvailableQuery(deptId, keyword)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AvailableDocumentResponse>> ListAvailableAsync(
        Guid deptId,
        string? keyword,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return await AvailableQuery(deptId, keyword)
            .OrderBy(item => item.DocumentNo)
            .ThenBy(item => item.DocumentId)
            .Skip(skip)
            .Take(take)
            .Select(item => new AvailableDocumentResponse(
                item.DocumentId,
                item.DocumentNo,
                item.DocumentName,
                item.CompanyName,
                new AvailableDocumentVersionResponse(
                    item.VersionId,
                    item.Version,
                    item.EffectiveDate,
                    item.PageCount)))
            .ToListAsync(cancellationToken);
    }

    public Task<DocumentDownloadRecord?> FindDocumentDownloadAsync(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken) =>
        (from document in dbContext.Documents.AsNoTracking()
         join version in dbContext.DocumentVersions.AsNoTracking()
             on document.Id equals version.DocumentId
         where document.Id == documentId
             && version.Id == versionId
         select new DocumentDownloadRecord(
             document.CompanyId,
             version.Id,
             version.Status,
             version.FileKey,
             version.OriginalFileName,
             version.ContentType))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<AttachmentDownloadRecord?> FindAttachmentDownloadAsync(
        Guid attachmentId,
        Guid versionId,
        CancellationToken cancellationToken) =>
        (from attachment in dbContext.Attachments.AsNoTracking()
         join document in dbContext.Documents.AsNoTracking()
             on attachment.DocumentId equals document.Id
         join version in dbContext.AttachmentVersions.AsNoTracking()
             on attachment.Id equals version.AttachmentId
         where attachment.Id == attachmentId
             && attachment.IsActive
             && version.Id == versionId
         select new AttachmentDownloadRecord(
             document.CompanyId,
             attachment.Id,
             version.Status,
             version.FileKey,
             version.OriginalFileName,
             version.ContentType))
        .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<AvailableDocumentQueryItem> AvailableQuery(
        Guid deptId,
        string? keyword)
    {
        var query =
            from permission in dbContext.DocumentDeptPermissions.AsNoTracking()
            join document in dbContext.Documents.AsNoTracking()
                on permission.DocumentId equals document.Id
            join company in dbContext.Companies.AsNoTracking()
                on document.CompanyId equals company.Id
            join version in dbContext.DocumentVersions.AsNoTracking()
                on document.Id equals version.DocumentId
            where permission.DeptId == deptId
                && document.IsActive
                && version.Status == "PUBLISHED"
            select new AvailableDocumentQueryItem
            {
                DocumentId = document.Id,
                DocumentNo = document.DocumentNo,
                DocumentName = document.Name,
                CompanyName = company.Name,
                VersionId = version.Id,
                Version = version.Version,
                EffectiveDate = version.EffectiveDate,
                PageCount = version.PageCount
            };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";

            query = query.Where(item =>
                EF.Functions.ILike(item.DocumentNo, pattern)
                || EF.Functions.ILike(item.DocumentName, pattern));
        }

        return query;
    }

    private sealed class AvailableDocumentQueryItem
    {
        public Guid DocumentId { get; init; }

        public string DocumentNo { get; init; } = string.Empty;

        public string DocumentName { get; init; } = string.Empty;

        public string CompanyName { get; init; } = string.Empty;

        public Guid VersionId { get; init; }

        public string Version { get; init; } = string.Empty;

        public DateOnly? EffectiveDate { get; init; }

        public int? PageCount { get; init; }
    }
}