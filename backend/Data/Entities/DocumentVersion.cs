namespace IsoDocument.Api.Data.Entities;

public sealed class DocumentVersion
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string Version { get; set; } = string.Empty;
    public int VersionMajor { get; set; }
    public int VersionMinor { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? PublishDate { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public int? PageCount { get; set; }
    public string? Memo { get; set; }
    public string? FileKey { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public string? Checksum { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
