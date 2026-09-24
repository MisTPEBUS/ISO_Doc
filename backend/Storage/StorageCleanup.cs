namespace IsoDocument.Api.Storage;

public static class StorageCleanup
{
    public static async Task TryMoveToTrashAsync(
        IDocumentStorage storage,
        string objectKey,
        ILogger logger)
    {
        try
        {
            await storage.MoveToTrashAsync(objectKey, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Failed to move uncommitted storage object {ObjectKey} to trash; manual reconciliation is required.",
                objectKey);
        }
    }
}
