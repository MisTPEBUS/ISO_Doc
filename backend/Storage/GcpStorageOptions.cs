namespace IsoDocument.Api.Storage;

public sealed class GcpStorageOptions
{
    public const string SectionName = "Gcp";

    public string ObjectPrefix { get; init; } = "documents/iso";
}
