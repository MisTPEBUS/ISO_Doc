namespace IsoDocument.Api.Features.Users.Dtos;

public sealed record BatchCreateUsersRequest(IReadOnlyList<CreateUserRequest> Users);
