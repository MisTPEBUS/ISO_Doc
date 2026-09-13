using IsoDocument.Api.Common;
using IsoDocument.Api.Data;
using IsoDocument.Api.Features.Permissions.Dtos;
using IsoDocument.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.Permissions;

public sealed class DocumentPermissionMatrixService(
    IsoDbContext dbContext,
    Storage.IDocumentStorage documentStorage,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IDocumentPermissionMatrixService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    public async Task<Result<DocumentPermissionMatrixResponse>> GetMatrixAsync(
        DocumentPermissionMatrixQuery query,
        CancellationToken cancellationToken)
    {
        // 前端可用 companyId 或 companyCode 指定公司；companyCode 僅在未帶 companyId 時採用，
        // 先解析為 companyId。指定了不存在的 companyCode → 用 Guid.Empty（不 match 任何公司）。
        var requestedCompanyId = query.CompanyId;
        if (requestedCompanyId is null && !string.IsNullOrWhiteSpace(query.CompanyCode))
        {
            var companyCode = query.CompanyCode.Trim().ToLowerInvariant();
            requestedCompanyId = await dbContext.Companies
                .Where(company => company.Code.ToLower() == companyCode)
                .Select(company => (Guid?)company.Id)
                .SingleOrDefaultAsync(cancellationToken)
                ?? Guid.Empty;
        }

        // Authorization scope：一律從 ICurrentUser 取得，不信任 query string 傳入的公司參數。
        // - COMPANY_ADMIN：強制為自己的公司，query 帶的 companyId / companyCode 一律忽略。
        // - SYSTEM_ADMIN：可用 companyId / companyCode 篩選，不帶則查全部公司（scopeCompanyId = null）。
        // 角色本身已由 [Authorize(Policy = CompanyAdminScope)] 限縮為這兩者。
        Guid? scopeCompanyId;
        switch (currentUser.Role)
        {
            case UserRole.SYSTEM_ADMIN:
                scopeCompanyId = requestedCompanyId;
                break;
            case UserRole.COMPANY_ADMIN when currentUser.CompanyId is { } ownCompanyId:
                scopeCompanyId = ownCompanyId;
                break;
            default:
                return Result<DocumentPermissionMatrixResponse>.Forbidden(
                    "無法判斷您的公司範圍。");
        }
        var page = query.Page > 0 ? query.Page : DefaultPage;
        var pageSize = query.PageSize > 0
            ? Math.Min(query.PageSize, MaximumPageSize)
            : DefaultPageSize;
        var keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();

        // (1) departments —— 依 company_id、seq 排序，僅回傳查詢範圍內公司的部門
        var departmentsQuery = dbContext.Depts.AsNoTracking();
        if (scopeCompanyId is { } deptCompanyId)
        {
            departmentsQuery = departmentsQuery.Where(dept => dept.CompanyId == deptCompanyId);
        }

        var departments = await departmentsQuery
            .OrderBy(dept => dept.CompanyId)
            .ThenBy(dept => dept.Seq)
            .ThenBy(dept => dept.Name)
            .Select(dept => new DepartmentOptionResponse(dept.Id, dept.Name, dept.Seq))
            .ToListAsync(cancellationToken);

        // (2) documents —— 依 document_no ASC 排序，分頁
        var documentsQuery = dbContext.Documents.AsNoTracking();
        if (scopeCompanyId is { } documentCompanyId)
        {
            documentsQuery = documentsQuery.Where(document => document.CompanyId == documentCompanyId);
        }

        if (keyword is not null)
        {
            var loweredKeyword = keyword.ToLowerInvariant();
            documentsQuery = documentsQuery.Where(document =>
                document.DocumentNo.ToLower().Contains(loweredKeyword)
                || document.Name.ToLower().Contains(loweredKeyword));
        }

        var totalCount = await documentsQuery.CountAsync(cancellationToken);
        var pageDocuments = await documentsQuery
            .OrderBy(document => document.DocumentNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(document => new PageDocument(
                document.Id, document.CompanyId, document.DocumentNo, document.Name, document.IsActive))
            .ToListAsync(cancellationToken);
        var pageDocumentIds = pageDocuments.Select(document => document.Id).ToArray();

        // (3) document_dept_permissions —— 一次撈當頁全部後在記憶體聚合成 departmentIds（避免 N+1）
        var permissionRows = await dbContext.DocumentDeptPermissions.AsNoTracking()
            .Where(permission => pageDocumentIds.Contains(permission.DocumentId))
            .Select(permission => new { permission.DocumentId, permission.DeptId })
            .ToListAsync(cancellationToken);
        var departmentIdsByDocument = permissionRows
            .GroupBy(row => row.DocumentId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<Guid>)group
                    .Select(row => row.DeptId)
                    .Distinct()
                    .ToArray());

        // 版本 —— 一次撈當頁全部，決定每份文件的代表版本（PUBLISHED 優先，否則 DRAFT，否則視同 DRAFT）
        var versionRows = await dbContext.DocumentVersions.AsNoTracking()
            .Where(version => pageDocumentIds.Contains(version.DocumentId))
            .Select(version => new RepresentativeVersion(
                version.Id,
                version.DocumentId,
                version.Status,
                version.Version,
                version.EffectiveDate,
                version.FileKey))
            .ToListAsync(cancellationToken);
        var representativeVersionByDocument = versionRows
            .GroupBy(version => version.DocumentId)
            .ToDictionary(
                group => group.Key,
                group => group.FirstOrDefault(version => version.Status == "PUBLISHED")
                    ?? group.FirstOrDefault(version => version.Status == "DRAFT"));

        // 附件已獨立編版，不再從屬於主文的代表版本：每個附件身份各自依「代表版本」規則
        // （PUBLISHED 優先，否則 DRAFT）取得自己的檔案狀態，再依 document_id 彙總。
        var attachmentIdentityRows = await dbContext.Attachments.AsNoTracking()
            .Where(attachment => pageDocumentIds.Contains(attachment.DocumentId) && attachment.IsActive)
            .Select(attachment => new { attachment.Id, attachment.DocumentId })
            .ToListAsync(cancellationToken);
        var attachmentIdsByDocument = attachmentIdentityRows
            .GroupBy(row => row.DocumentId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.Id).ToArray());

        var allAttachmentIds = attachmentIdentityRows.Select(row => row.Id).ToArray();
        var attachmentVersionRows = await dbContext.AttachmentVersions.AsNoTracking()
            .Where(version => allAttachmentIds.Contains(version.AttachmentId))
            .Select(version => new { version.AttachmentId, version.Status, version.FileKey })
            .ToListAsync(cancellationToken);
        var representativeFileKeyByAttachment = attachmentVersionRows
            .GroupBy(row => row.AttachmentId)
            .ToDictionary(
                group => group.Key,
                group => (group.FirstOrDefault(row => row.Status == "PUBLISHED")
                    ?? group.FirstOrDefault(row => row.Status == "DRAFT"))?.FileKey);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var items = new List<DocumentPermissionRowResponse>(pageDocuments.Count);
        foreach (var document in pageDocuments)
        {
            representativeVersionByDocument.TryGetValue(document.Id, out var version);

            var mainDocumentStatus = await BuildMainDocumentStatusAsync(
                version?.FileKey, cancellationToken);
            var documentAttachmentIds = attachmentIdsByDocument.GetValueOrDefault(document.Id);
            var attachmentStatus = await BuildAttachmentStatusAsync(
                documentAttachmentIds?
                    .Select(id => representativeFileKeyByAttachment.GetValueOrDefault(id))
                    .ToArray(),
                cancellationToken);
            var effectiveStatus = BuildEffectiveStatus(
                document.IsActive, version?.Status, version?.EffectiveDate, today);

            items.Add(new DocumentPermissionRowResponse(
                document.Id,
                document.DocumentNo,
                document.Name,
                version?.Version,
                document.CompanyId,
                new DocumentStatusResponse(mainDocumentStatus, attachmentStatus, effectiveStatus),
                departmentIdsByDocument.GetValueOrDefault(document.Id, Array.Empty<Guid>())));
        }

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<DocumentPermissionMatrixResponse>.Success(new DocumentPermissionMatrixResponse(
            departments,
            items,
            new PaginationResponse(page, pageSize, totalCount, totalPages)));
    }

    private async Task<DocumentFileStatusResponse> BuildMainDocumentStatusAsync(
        string? fileKey,
        CancellationToken cancellationToken)
    {
        // 代表版本為 null 或 DRAFT（無主檔）→ MISSING。
        if (fileKey is null)
        {
            return DocumentStatusCatalog.MainDocument(DocumentStatusCatalog.MainDocumentCode.Missing);
        }

        // 對當頁文件即時檢查實體檔案是否存在（不做背景快取）。
        var exists = await documentStorage.ExistsAsync(fileKey, cancellationToken);
        return DocumentStatusCatalog.MainDocument(exists
            ? DocumentStatusCatalog.MainDocumentCode.Normal
            : DocumentStatusCatalog.MainDocumentCode.Error);
    }

    private async Task<DocumentFileStatusResponse> BuildAttachmentStatusAsync(
        IReadOnlyList<string?>? attachmentFileKeys,
        CancellationToken cancellationToken)
    {
        if (attachmentFileKeys is null || attachmentFileKeys.Count == 0)
        {
            return DocumentStatusCatalog.Attachment(DocumentStatusCatalog.AttachmentCode.None);
        }

        var anyMissing = false;
        var anyError = false;
        foreach (var fileKey in attachmentFileKeys)
        {
            if (fileKey is null)
            {
                anyMissing = true;
                continue;
            }

            if (!await documentStorage.ExistsAsync(fileKey, cancellationToken))
            {
                anyError = true;
            }
        }

        // ERROR（實體檔遺失）優先於 MISSING（僅中繼資料、待補檔）。
        var code = anyError
            ? DocumentStatusCatalog.AttachmentCode.Error
            : anyMissing
                ? DocumentStatusCatalog.AttachmentCode.Missing
                : DocumentStatusCatalog.AttachmentCode.Normal;
        return DocumentStatusCatalog.Attachment(code);
    }

    private static DocumentEffectiveStatusResponse BuildEffectiveStatus(
        bool documentIsActive,
        string? versionStatus,
        DateOnly? effectiveDate,
        DateOnly today)
    {
        // CANCELLED 直接讀 Document.is_active，優先於其他判斷。
        if (!documentIsActive)
        {
            return DocumentStatusCatalog.Effective(DocumentStatusCatalog.EffectiveCode.Cancelled);
        }

        // 代表版本為 null / DRAFT / 其他（OBSOLETE 不會成為代表版本）→ 視同草稿。
        if (versionStatus != "PUBLISHED")
        {
            return DocumentStatusCatalog.Effective(DocumentStatusCatalog.EffectiveCode.Draft);
        }

        if (effectiveDate is { } effective && effective > today)
        {
            return DocumentStatusCatalog.Effective(DocumentStatusCatalog.EffectiveCode.Scheduled);
        }

        // EXPIRED 目前邏輯上不會產生（見 SPEC「Document 狀態計算」），一律回 EFFECTIVE。
        return DocumentStatusCatalog.Effective(DocumentStatusCatalog.EffectiveCode.Effective);
    }

    private sealed record PageDocument(
        Guid Id,
        Guid CompanyId,
        string DocumentNo,
        string Name,
        bool IsActive);

    private sealed record RepresentativeVersion(
        Guid Id,
        Guid DocumentId,
        string Status,
        string Version,
        DateOnly? EffectiveDate,
        string? FileKey);
}
