namespace IsoDocument.Api.Features.AiImport.Dtos;

public sealed class AnalyzeImportRequest
{
    public Guid CompanyId { get; init; }
    public IReadOnlyList<ImportFileDescriptor>? Files { get; init; }
}

public sealed class ImportFileDescriptor
{
    public string? OriginalFileName { get; init; }
    public string? RelativePath { get; init; }
    public long Size { get; init; }
    public string? Role { get; init; }
    public string? DocumentNo { get; init; }
    public string? AttachmentNo { get; init; }
    public string? DisplayName { get; init; }
    public string? Extension { get; init; }
    public string? ParseStatus { get; init; }
    public string? Checksum { get; init; }
}

public static class ImportFileRoles
{
    public const string Main = "MAIN";
    public const string Attachment = "ATTACHMENT";
    public const string MainCandidate = "MAIN_CANDIDATE";
    public const string Unresolved = "UNRESOLVED";
}

public static class ImportParseStatuses
{
    public const string Ok = "OK";
    public const string Warning = "WARNING";
}
