using IsoDocument.Api.Common;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Home.Dtos;
using IsoDocument.Api.Security;
using IsoDocument.Api.Storage;

namespace IsoDocument.Api.Features.Home;

public sealed class DocumentsBrowseService(
    IDocumentsBrowseStore browseStore,
    IDocumentStorage documentStorage,
    IDownloadAuditLogService auditLogService,
    ICurrentUser currentUser) : IDocumentsBrowseService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    public async Task<Result<PagedResult<AvailableDocumentResponse>>> ListAvailableAsync(
        int page,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        if (currentUser.DeptId is not { } deptId)
        {
            return Result<PagedResult<AvailableDocumentResponse>>.Unauthorized(
                "請先登入後再操作。");
        }

        page = page > 0 ? page : DefaultPage;
        pageSize = pageSize > 0 ? Math.Min(pageSize, MaximumPageSize) : DefaultPageSize;
        var totalCount = await browseStore.CountAvailableAsync(
            deptId, keyword, cancellationToken);
        var documents = await browseStore.ListAvailableAsync(
            deptId, keyword, (page - 1) * pageSize, pageSize, cancellationToken);
        return Result<PagedResult<AvailableDocumentResponse>>.Success(new(
            documents, page, pageSize, totalCount));
    }

    public async Task<Result<DownloadFileResponse>> DownloadDocumentAsync(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var record = await browseStore.FindDocumentDownloadAsync(
            documentId, versionId, cancellationToken);
        if (record is null || !DownloadAccessRules.CanDownload(currentUser.Role, record.VersionStatus))
        {
            return Result<DownloadFileResponse>.Forbidden("您沒有下載此檔案的權限。");
        }

        if (record.FileKey is null
            || !await documentStorage.ExistsAsync(record.FileKey, cancellationToken))
        {
            return Result<DownloadFileResponse>.NotFound("文件檔案尚未上傳。");
        }

        var stream = await documentStorage.OpenReadAsync(record.FileKey, cancellationToken);
        await auditLogService.WriteDocumentDownloadedAsync(
            record.CompanyId, record.VersionId, cancellationToken);
        return Result<DownloadFileResponse>.Success(new(
            stream,
            record.ContentType ?? "application/pdf",
            record.OriginalFileName ?? "document.pdf"));
    }

    public async Task<Result<DownloadFileResponse>> DownloadAttachmentAsync(
        Guid attachmentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var record = await browseStore.FindAttachmentDownloadAsync(
            attachmentId, versionId, cancellationToken);
        if (record is null || !DownloadAccessRules.CanDownload(currentUser.Role, record.VersionStatus))
        {
            return Result<DownloadFileResponse>.Forbidden("您沒有下載此檔案的權限。");
        }

        if (record.FileKey is null
            || !await documentStorage.ExistsAsync(record.FileKey, cancellationToken))
        {
            return Result<DownloadFileResponse>.NotFound("表單及附件檔案尚未上傳。");
        }

        var stream = await documentStorage.OpenReadAsync(record.FileKey, cancellationToken);
        await auditLogService.WriteAttachmentDownloadedAsync(
            record.CompanyId, record.AttachmentId, cancellationToken);
        return Result<DownloadFileResponse>.Success(new(
            stream,
            record.ContentType ?? "application/octet-stream",
            record.OriginalFileName ?? "attachment"));
    }
}
