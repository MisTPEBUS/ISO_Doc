namespace IsoDocument.Api.Data.Entities;

public sealed class AttachmentVersion
{
    public Guid Id { get; set; }

    public Guid AttachmentId { get; set; }

    public string Version { get; set; } = null!;

    public int VersionMajor { get; set; }

    public int VersionMinor { get; set; }

    public string Status { get; set; } = null!;

    public DateOnly? PublishDate { get; set; }

    public DateOnly? EffectiveDate { get; set; }

    public DateOnly? ExpiredDate { get; set; }

    public string? FileKey { get; set; }

    public string? OriginalFileName { get; set; }

    public string? ContentType { get; set; }

    public long? FileSize { get; set; }

    public string? Checksum { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
