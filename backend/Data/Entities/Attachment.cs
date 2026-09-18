namespace IsoDocument.Api.Data.Entities;

public sealed class Attachment
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public string? AttachmentNo { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}