using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.Documents;

public interface IDocumentStore
{
    Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken);
    Task<bool> DocumentNoExistsAsync(
        Guid companyId, string documentNo, CancellationToken cancellationToken);
    Task<int> CountAsync(Guid? companyId, string? keyword, CancellationToken cancellationToken);
    Task<IReadOnlyList<Document>> ListAsync(
        Guid? companyId, string? keyword, int skip, int take,
        CancellationToken cancellationToken);
    Task<Document?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentVersion>> ListVersionsAsync(
        Guid documentId, CancellationToken cancellationToken);
    void Add(Document document);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
