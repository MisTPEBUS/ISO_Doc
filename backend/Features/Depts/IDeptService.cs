using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Depts.Dtos;

namespace IsoDocument.Api.Features.Depts;

public interface IDeptService
{
    Task<Result<PagedResult<DeptResponse>>> ListAsync(
        Guid? companyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Result<DeptResponse>> CreateAsync(
        CreateDeptRequest request,
        CancellationToken cancellationToken);

    Task<Result<DeptResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<DeptResponse>> UpdateAsync(
        Guid id,
        UpdateDeptRequest request,
        CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
