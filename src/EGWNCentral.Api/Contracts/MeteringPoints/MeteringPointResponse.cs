namespace EGWNCentral.Api.Contracts.MeteringPoints;

public sealed record MeteringPointResponse(
    Guid Id,
    IReadOnlyCollection<Guid> CustomerIds,
    string MeteringPointCode,
    string EnergyDirection,
    string? Description,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
