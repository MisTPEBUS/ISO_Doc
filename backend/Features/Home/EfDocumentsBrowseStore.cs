using IsoDocument.Api.Data;
using IsoDocument.Api.Features.Home.Dtos;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.Home;

public sealed class EfDocumentsBrowseStore(IsoDbContext dbContext) : IDocumentsBrowseStore
{
    public Task<int> CountAvailableAsync(
        Guid deptId,
        string? keyword,
        Guid? isoCategoryId,
        CancellationToken cancellationToken)
    {
        return AvailableQuery(deptId, keyword, isoCategoryId)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AvailableDocumentResponse>> ListAvailableAsync(
        Guid deptId,
        string? keyword,
        Guid? isoCategoryId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var documents = await AvailableQuery(deptId, keyword, isoCategoryId)
            .OrderBy(item => item.DocumentNo)
            .ThenBy(item => item.DocumentId)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        if (documents.Count == 0)
        {
            return [];
        }

        var documentIds = documents.Select(item => item.DocumentId).ToArray();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var attachmentRows = await (
            from attachment in dbContext.Attachments.AsNoTracking()
            where documentIds.Contains(attachment.DocumentId)
                && attachment.IsActive
            join version in dbContext.AttachmentVersions.AsNoTracking().Where(version =>
                    version.Status == "PUBLISHED"
                    && version.EffectiveDate.HasValue
                    && version.EffectiveDate.Value <= today
                    && (!version.ExpiredDate.HasValue || version.ExpiredDate.Value > today))
                on attachment.Id equals version.AttachmentId into currentVersions
            from currentVersion in currentVersions.DefaultIfEmpty()
            orderby attachment.DocumentId, attachment.AttachmentNo, attachment.Id
            select new AvailableAttachmentQueryItem
            {
                DocumentId = attachment.DocumentId,
                AttachmentId = attachment.Id,
                AttachmentNo = attachment.AttachmentNo,
                Name = attachment.Name,
                VersionId = currentVersion == null ? null : currentVersion.Id,
                Version = currentVersion == null ? null : currentVersion.Version,
                HasFile = currentVersion != null && currentVersion.FileKey != null
            })
            .ToListAsync(cancellationToken);

        var attachmentsByDocumentId = attachmentRows
            .GroupBy(item => item.DocumentId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AvailableAttachmentResponse>)group
                    .Select(item => new AvailableAttachmentResponse(
                        item.AttachmentId,
                        item.AttachmentNo,
                        item.Name,
                        item.VersionId.HasValue && item.Version is not null
                            ? new AvailableAttachmentVersionResponse(
                                item.VersionId.Value,
                                item.Version,
                                item.HasFile)
                            : null))
                    .ToArray());

        return documents
            .Select(item => new AvailableDocumentResponse(
                item.DocumentId,
                item.DocumentNo,
                item.DocumentName,
                item.CompanyName,
                item.IsoCategoryId,
                item.IsoCategoryName,
                new AvailableDocumentVersionResponse(
                    item.VersionId,
                    item.Version,
                    item.EffectiveDate,
                    item.PageCount,
                    item.FileKey is not null),
                attachmentsByDocumentId.GetValueOrDefault(item.DocumentId) ?? []))
            .ToArray();
    }

    public Task<DocumentDownloadRecord?> FindDocumentDownloadAsync(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken) =>
        (from document in dbContext.Documents.AsNoTracking()
         join company in dbContext.Companies.AsNoTracking()
             on document.CompanyId equals company.Id
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
             version.ContentType,
             company.Code))
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
        string? keyword,
        Guid? isoCategoryId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query =
            from permission in dbContext.DocumentDeptPermissions.AsNoTracking()
            join document in dbContext.Documents.AsNoTracking()
                on permission.DocumentId equals document.Id
            join company in dbContext.Companies.AsNoTracking()
                on document.CompanyId equals company.Id
            join version in dbContext.DocumentVersions.AsNoTracking()
                on document.Id equals version.DocumentId
            join category in dbContext.IsoCategories.AsNoTracking()
                on document.IsoCategoryId equals category.Id into categories
            from category in categories.DefaultIfEmpty()
            where permission.DeptId == deptId
                && document.IsActive
                && version.Status == "PUBLISHED"
                && version.EffectiveDate.HasValue
                && version.EffectiveDate.Value <= today
                && (!version.ExpiredDate.HasValue || version.ExpiredDate.Value > today)
            select new AvailableDocumentQueryItem
            {
                DocumentId = document.Id,
                DocumentNo = document.DocumentNo,
                DocumentName = document.Name,
                CompanyName = company.Name,
                IsoCategoryId = document.IsoCategoryId,
                IsoCategoryName = category == null ? null : category.Name,
                VersionId = version.Id,
                Version = version.Version,
                EffectiveDate = version.EffectiveDate,
                PageCount = version.PageCount,
                FileKey = version.FileKey
            };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";

            query = query.Where(item =>
                EF.Functions.ILike(item.DocumentNo, pattern)
                || EF.Functions.ILike(item.DocumentName, pattern));
        }

        if (isoCategoryId.HasValue)
        {
            query = query.Where(item => item.IsoCategoryId == isoCategoryId.Value);
        }

        return query;
    }

    private sealed class AvailableDocumentQueryItem
    {
        public Guid DocumentId { get; init; }

        public string DocumentNo { get; init; } = string.Empty;

        public string DocumentName { get; init; } = string.Empty;

        public string CompanyName { get; init; } = string.Empty;

        public Guid? IsoCategoryId { get; init; }

        public string? IsoCategoryName { get; init; }

        public Guid VersionId { get; init; }

        public string Version { get; init; } = string.Empty;

        public DateOnly? EffectiveDate { get; init; }

        public int? PageCount { get; init; }

        public string? FileKey { get; init; }
    }

    private sealed class AvailableAttachmentQueryItem
    {
        public Guid DocumentId { get; init; }

        public Guid AttachmentId { get; init; }

        public string? AttachmentNo { get; init; }

        public string Name { get; init; } = string.Empty;

        public Guid? VersionId { get; init; }

        public string? Version { get; init; }

        public bool HasFile { get; init; }
    }
}
