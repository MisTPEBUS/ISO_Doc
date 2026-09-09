using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents;

public interface IAttachmentService
{
    Task<Result<IReadOnlyList<AttachmentResponse>>> ListAsync(
        Guid documentId, Guid versionId, CancellationToken cancellationToken);
    Task<Result<CreateAttachmentsResponse>> CreateAsync(
        Guid documentId, Guid versionId, CreateAttachmentsRequest request,
        CancellationToken cancellationToken);
    Task<Result> DeleteAsync(Guid attachmentId, CancellationToken cancellationToken);
}
