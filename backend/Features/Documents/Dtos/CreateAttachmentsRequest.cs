namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed class CreateAttachmentsRequest
{
    public List<CreateAttachmentItemRequest> Items { get; init; } = [];
}
