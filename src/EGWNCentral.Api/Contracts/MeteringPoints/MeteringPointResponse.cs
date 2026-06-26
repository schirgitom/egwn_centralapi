namespace EGWNCentral.Api.Contracts.MeteringPoints;

public sealed record MeteringPointResponse(
    Guid Id,
    Guid CustomerId,
    string MeteringPointCode,
    string? Description,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
