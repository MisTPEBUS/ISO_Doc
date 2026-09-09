namespace IsoDocument.Api.Features.Depts.Dtos;

public sealed record DeptResponse(
    Guid Id,
    Guid CompanyId,
    string Name,
    int? Seq,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
