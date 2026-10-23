namespace EGWNCentral.Api.Contracts.EnergyCommunities;

public sealed record EnergyCommunityResponse(
    Guid Id,
    string Name,
    string? Source,
    IReadOnlyCollection<CommunityParticipationResponse> Participations,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
