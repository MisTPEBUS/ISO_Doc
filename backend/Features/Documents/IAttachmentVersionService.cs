using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents;

public interface IAttachmentVersionService
{
    Task<Result<AttachmentVersionResponse>> CreateAsync(
        Guid attachmentId,
        CreateAttachmentVersionRequest request,
        CancellationToken cancellationToken);

    Task<Result<AttachmentVersionDetailResponse>> GetAsync(
        Guid attachmentId,
        Guid versionId,
        CancellationToken cancellationToken);
}
