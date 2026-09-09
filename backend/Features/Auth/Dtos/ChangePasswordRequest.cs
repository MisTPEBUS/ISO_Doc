namespace IsoDocument.Api.Features.Auth.Dtos;

public sealed record ChangePasswordRequest(
    string? CurrentPassword,
    string? NewPassword,
    string? NewPasswordConfirmation);
