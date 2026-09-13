using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Home.Dtos;

namespace IsoDocument.Api.Features.Home;

public interface IDocumentsBrowseService
{
    Task<Result<PagedResult<AvailableDocumentResponse>>> ListAvailableAsync(
        int page, int pageSize, string? keyword, CancellationToken cancellationToken);
    Task<Result<DownloadFileResponse>> DownloadDocumentAsync(
        Guid documentId, Guid versionId, CancellationToken cancellationToken);
    Task<Result<DownloadFileResponse>> DownloadAttachmentAsync(
        Guid attachmentId, Guid versionId, CancellationToken cancellationToken);
}
