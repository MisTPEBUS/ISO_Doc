using IsoDocument.Api.Storage;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsoDocs.Tests.Storage;

public sealed class StorageCleanupTests
{
    [Fact]
    public async Task TryMoveToTrashAsync_WhenCleanupFails_LogsObjectKeyAndPreservesOriginalFailure()
    {
        var storage = new FailingStorage();
        var logger = new RecordingLogger();
        const string key = "documents/iso/company/category/document/main/v1.0/file.pdf";

        await StorageCleanup.TryMoveToTrashAsync(storage, key, logger);

        Assert.Equal(key, storage.AttemptedKey);
        Assert.Contains(logger.Messages, message =>
            message.Contains(key, StringComparison.Ordinal));
    }

    private sealed class FailingStorage : IDocumentStorage
    {
        public string? AttemptedKey { get; private set; }

        public Task MoveToTrashAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            AttemptedKey = objectKey;
            throw new IOException("GCP move failed");
        }

        public Task<StorageWriteResult> WriteAsync(
            string objectKey, Stream content, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(
            string objectKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsAsync(
            string objectKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
