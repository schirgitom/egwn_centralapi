namespace EGWNCentral.Api.Contracts.EnergyCommunities;

public sealed record CommunityParticipationResponse(
    Guid MeteringPointId,
    string MeteringPointCode,
    string CustomerNumber,
    string CustomerName,
    decimal ParticipationPercentage);
