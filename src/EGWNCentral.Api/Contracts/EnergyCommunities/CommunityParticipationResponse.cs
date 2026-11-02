namespace EGWNCentral.Api.Contracts.EnergyCommunities;

public sealed record CommunityParticipationResponse(
    Guid MeteringPointId,
    string MeteringPointCode,
    IReadOnlyCollection<ParticipationCustomerResponse> Customers,
    decimal ParticipationPercentage);

public sealed record ParticipationCustomerResponse(Guid Id, string CustomerNumber, string Name);
