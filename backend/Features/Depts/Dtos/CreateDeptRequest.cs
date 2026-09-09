namespace IsoDocument.Api.Features.Depts.Dtos;

public sealed record CreateDeptRequest(Guid CompanyId, string? Name, int? Seq);
