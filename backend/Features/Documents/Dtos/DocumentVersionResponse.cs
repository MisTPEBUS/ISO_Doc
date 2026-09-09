namespace IsoDocument.Api.Features.Documents.Dtos;

public sealed record DocumentVersionResponse(Guid VersionId, string Version, string Status);
