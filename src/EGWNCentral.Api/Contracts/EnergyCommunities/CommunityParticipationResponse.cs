namespace EGWNCentral.Api.Contracts.EnergyCommunities;

public sealed record CommunityParticipationResponse(
    Guid MeteringPointId,
    string MeteringPointCode,
    string EnergyDirection,
    IReadOnlyCollection<ParticipationCustomerResponse> Customers,
    decimal ParticipationPercentage);

public sealed record ParticipationCustomerResponse(Guid Id, string CustomerNumber, string Name);
