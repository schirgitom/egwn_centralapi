namespace EGWNCentral.Api.Contracts.MeteringPoints;

public sealed record MeteringPointResponse(
    Guid Id,
    Guid CustomerId,
    string MeteringPointCode,
    string EnergyDirection,
    string? Description,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
