using IsoDocument.Api.Common;
using IsoDocument.Api.Features.Auth.Dtos;

namespace IsoDocument.Api.Features.Auth;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken);

    Task<Result> LogoutAsync();

    Task<Result<MeResponse>> GetCurrentUserAsync(CancellationToken cancellationToken);

    Task<Result> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken);
}
