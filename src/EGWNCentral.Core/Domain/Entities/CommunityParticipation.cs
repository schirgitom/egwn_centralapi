namespace EGWNCentral.Core.Domain.Entities;

public sealed class CommunityParticipation : SoftDeletableEntity
{
    public Guid EnergyCommunityId { get; set; }

    public Guid MeteringPointId { get; set; }

    public decimal ParticipationPercentage { get; set; }

    public EnergyCommunity EnergyCommunity { get; set; } = null!;

    public MeteringPoint MeteringPoint { get; set; } = null!;
}
