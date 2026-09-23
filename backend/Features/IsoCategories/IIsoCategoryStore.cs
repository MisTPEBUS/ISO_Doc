using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.IsoCategories;

public interface IIsoCategoryStore
{
    Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(
        Guid companyId,
        string name,
        Guid? excludingIsoCategoryId,
        CancellationToken cancellationToken);

    Task<int> CountAsync(
        Guid? companyId, bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyList<IsoCategory>> ListAsync(
        Guid? companyId,
        bool includeInactive,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<IsoCategory?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(IsoCategory isoCategory);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
