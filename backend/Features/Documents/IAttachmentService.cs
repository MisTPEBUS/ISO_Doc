using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents;

public interface IAttachmentService
{
    Task<Result<IReadOnlyList<AttachmentResponse>>> ListAsync(
        Guid documentId, CancellationToken cancellationToken);
    Task<Result<AttachmentResponse>> CreateAsync(
        Guid documentId, CreateAttachmentRequest request, CancellationToken cancellationToken);
    Task<Result<BulkImportAttachmentsResponse>> BulkImportAsync(
        BulkImportAttachmentsRequest request, CancellationToken cancellationToken);
    Task<Result<AttachmentDetailResponse>> GetAsync(
        Guid documentId, Guid attachmentId, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(
        Guid documentId, Guid attachmentId, CancellationToken cancellationToken);
}
