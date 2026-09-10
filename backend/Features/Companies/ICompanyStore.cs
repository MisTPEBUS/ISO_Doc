using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Features.Companies;

public interface ICompanyStore
{
    Task<int> CountAsync(string? keyword, CancellationToken cancellationToken);

    Task<IReadOnlyList<Company>> ListAsync(
        string? keyword,
        int skip,
        int take,
        CancellationToken cancellationToken);
}
