namespace EGWNCentral.Core.Domain.Entities;

public sealed class MeteringPoint : SoftDeletableEntity
{
    public string MeteringPointCode { get; set; } = string.Empty;

    public string EnergyDirection { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public ICollection<CommunityParticipation> CommunityParticipations { get; set; } = new List<CommunityParticipation>();
}
