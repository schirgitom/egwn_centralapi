namespace EGWNCentral.Infrastructure.Messaging;

public sealed record CustomerSnapshot(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    DateTimeOffset OccurredAtUtc,
    CustomerMasterData Customer)
{
    // Keep the existing binding so configured consumers also receive subsequent snapshots.
    public const string RoutingKey = "customer.created.v1";
}

public sealed record CustomerMasterData(
    Guid Id, string CustomerNumber, string RegistrationCode, string Name, string? Email, bool IsDeleted,
    IReadOnlyList<CustomerMeteringPoint> MeteringPoints,
    IReadOnlyList<CustomerEnergyCommunity> EnergyCommunities);

public sealed record CustomerMeteringPoint(
    Guid Id, string MeteringPointCode, string EnergyDirection, string? Description);

public sealed record CustomerEnergyCommunity(
    Guid Id, string Name, string? Source, string? SourceId,
    IReadOnlyList<CustomerCommunityMeteringPoint> MeteringPoints);

public sealed record CustomerCommunityMeteringPoint(
    Guid Id, string MeteringPointCode, string EnergyDirection, string? Description,
    decimal ParticipationPercentage);
