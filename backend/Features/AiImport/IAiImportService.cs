using IsoDocument.Api.Common;
using IsoDocument.Api.Features.AiImport.Dtos;

namespace IsoDocument.Api.Features.AiImport;

public interface IAiImportService
{
    Task<Result<AnalyzeImportResponse>> AnalyzeAsync(
        AnalyzeImportRequest request, CancellationToken cancellationToken);

    Task<Result<CommitImportResponse>> CommitAsync(
        CommitImportRequest request, CancellationToken cancellationToken);
}
