namespace IsoDocument.Api.Storage;

public sealed class GcpStorageUnavailableException(Exception innerException)
    : Exception("GCP storage is temporarily unavailable.", innerException);
