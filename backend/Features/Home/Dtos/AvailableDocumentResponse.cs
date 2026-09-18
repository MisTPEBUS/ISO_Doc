namespace IsoDocument.Api.Features.Home.Dtos;

public sealed record AvailableDocumentResponse(
    Guid DocumentId,
    string DocumentNo,
    string Name,
    string CompanyName,
    AvailableDocumentVersionResponse CurrentVersion,
    IReadOnlyList<AvailableAttachmentResponse> Attachments);

public sealed record AvailableDocumentVersionResponse(
    Guid VersionId,
    string Version,
    DateOnly? EffectiveDate,
    int? PageCount,
    bool HasFile);

public sealed record AvailableAttachmentResponse(
    Guid AttachmentId,
    string? AttachmentNo,
    string Name,
    AvailableAttachmentVersionResponse? CurrentVersion);

public sealed record AvailableAttachmentVersionResponse(
    Guid VersionId,
    string Version,
    bool HasFile);
