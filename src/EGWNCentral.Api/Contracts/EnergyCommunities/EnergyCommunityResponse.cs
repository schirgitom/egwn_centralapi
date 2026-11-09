namespace EGWNCentral.Api.Contracts.EnergyCommunities;

public sealed record EnergyCommunityResponse(
    Guid Id,
    string Name,
    string? Source,
    string? SourceId,
    IReadOnlyCollection<CommunityParticipationResponse> Participations,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
