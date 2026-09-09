namespace IsoDocument.Api.Features.Home.Dtos;

public sealed record DownloadFileResponse(
    Stream Content,
    string ContentType,
    string FileName);
