namespace IsoDocument.Api.Features.Users.Dtos;

public sealed record BatchCreateUsersResponse(
    int Total,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<BatchCreateUserSuccess> Succeeded,
    IReadOnlyList<BatchCreateUserFailure> Failed);

public sealed record BatchCreateUserSuccess(int Index, UserResponse User);

public sealed record BatchCreateUserFailure(
    int Index,
    CreateUserRequest OriginalData,
    Dictionary<string, string[]> Errors);
