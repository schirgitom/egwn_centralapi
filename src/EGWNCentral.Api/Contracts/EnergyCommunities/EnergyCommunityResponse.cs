namespace EGWNCentral.Api.Contracts.EnergyCommunities;

public sealed record EnergyCommunityResponse(
    Guid Id,
    string Name,
    IReadOnlyCollection<CommunityParticipationResponse> Participations,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
