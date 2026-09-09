using IsoDocument.Api.Data;
using IsoDocument.Api.Features.Home.Dtos;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.Home;

public sealed class EfDocumentsBrowseStore(IsoDbContext dbContext) : IDocumentsBrowseStore
{
    public Task<int> CountAvailableAsync(
        Guid deptId,
        string? keyword,
        CancellationToken cancellationToken) =>
        AvailableQuery(deptId, keyword).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<AvailableDocumentResponse>> ListAvailableAsync(
        Guid deptId,
        string? keyword,
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        await AvailableQuery(deptId, keyword)
            .OrderBy(item => item.DocumentNo)
            .ThenBy(item => item.DocumentId)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<DocumentDownloadRecord?> FindDocumentDownloadAsync(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken) =>
        (from document in dbContext.Documents.AsNoTracking()
         join version in dbContext.DocumentVersions.AsNoTracking()
             on document.Id equals version.DocumentId
         where document.Id == documentId && version.Id == versionId
         select new DocumentDownloadRecord(
             document.CompanyId,
             version.Id,
             version.Status,
             version.FileKey,
             version.OriginalFileName,
             version.ContentType))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<AttachmentDownloadRecord?> FindAttachmentDownloadAsync(
        Guid documentId,
        Guid versionId,
        Guid attachmentId,
        CancellationToken cancellationToken) =>
        (from document in dbContext.Documents.AsNoTracking()
         join version in dbContext.DocumentVersions.AsNoTracking()
             on document.Id equals version.DocumentId
         join attachment in dbContext.Attachments.AsNoTracking()
             on version.Id equals attachment.DocumentVersionId
         where document.Id == documentId
             && version.Id == versionId
             && attachment.Id == attachmentId
         select new AttachmentDownloadRecord(
             document.CompanyId,
             attachment.Id,
             version.Status,
             attachment.FileKey,
             attachment.OriginalFileName,
             attachment.ContentType))
        .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<AvailableDocumentResponse> AvailableQuery(Guid deptId, string? keyword)
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
            select new AvailableDocumentResponse(
                document.Id,
                document.DocumentNo,
                document.Name,
                company.Name,
                new AvailableDocumentVersionResponse(
                    version.Id,
                    version.Version,
                    version.EffectiveDate,
                    version.PageCount));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.DocumentNo, pattern)
                || EF.Functions.ILike(item.Name, pattern));
        }

        return query;
    }
}
