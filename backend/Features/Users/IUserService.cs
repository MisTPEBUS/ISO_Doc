using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Users.Dtos;

namespace IsoDocument.Api.Features.Users;

public interface IUserService
{
    Task<Result<PagedResult<UserResponse>>> ListAsync(
        Guid? companyId, Guid? deptId, string? keyword, bool includeInactive,
        int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<Result<BatchCreateUsersResponse>> BatchCreateAsync(
        BatchCreateUsersRequest request, CancellationToken cancellationToken);
    Task<Result<UserResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<UserResponse>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<ResetPasswordResponse>> ResetPasswordAsync(Guid id, CancellationToken cancellationToken);
}
