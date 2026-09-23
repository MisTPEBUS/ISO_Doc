using IsoDocument.Api.Common;
using IsoDocument.Api.Features.IsoCategories.Dtos;

namespace IsoDocument.Api.Features.IsoCategories;

public interface IIsoCategoryService
{
    Task<Result<PagedResult<IsoCategoryResponse>>> ListAsync(
        Guid? companyId,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Result<IsoCategoryResponse>> CreateAsync(
        CreateIsoCategoryRequest request,
        CancellationToken cancellationToken);

    Task<Result<IsoCategoryResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<IsoCategoryResponse>> UpdateAsync(
        Guid id,
        UpdateIsoCategoryRequest request,
        CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
