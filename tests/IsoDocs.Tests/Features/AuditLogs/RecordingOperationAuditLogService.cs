using IsoDocument.Api.Features.AuditLogs;

namespace IsoDocs.Tests.Features.AuditLogs;

internal sealed class RecordingOperationAuditLogService : IOperationAuditLogService
{
    public List<AuditLogWriteRequest> Entries { get; } = [];

    public Task WriteAsync(
        AuditLogWriteRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Entries.Add(request);
        return Task.CompletedTask;
    }
}
