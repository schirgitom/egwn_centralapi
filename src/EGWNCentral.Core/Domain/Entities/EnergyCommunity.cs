namespace EGWNCentral.Core.Domain.Entities;

public sealed class EnergyCommunity : SoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Source { get; set; }

    public ICollection<CommunityParticipation> Participations { get; set; } = new List<CommunityParticipation>();
}
