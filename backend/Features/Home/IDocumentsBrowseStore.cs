using IsoDocument.Api.Features.Home.Dtos;

namespace IsoDocument.Api.Features.Home;

public sealed record DocumentDownloadRecord(
    Guid CompanyId,
    Guid VersionId,
    string VersionStatus,
    string? FileKey,
    string? OriginalFileName,
    string? ContentType,
    string CompanyCode);

public sealed record AttachmentDownloadRecord(
    Guid CompanyId,
    Guid AttachmentId,
    string VersionStatus,
    string? FileKey,
    string? OriginalFileName,
    string? ContentType);

public interface IDocumentsBrowseStore
{
    Task<int> CountAvailableAsync(
        Guid deptId, string? keyword, CancellationToken cancellationToken);
    Task<IReadOnlyList<AvailableDocumentResponse>> ListAvailableAsync(
        Guid deptId, string? keyword, int skip, int take,
        CancellationToken cancellationToken);
    Task<DocumentDownloadRecord?> FindDocumentDownloadAsync(
        Guid documentId, Guid versionId, CancellationToken cancellationToken);
    Task<AttachmentDownloadRecord?> FindAttachmentDownloadAsync(
        Guid attachmentId, Guid versionId, CancellationToken cancellationToken);
}
