namespace EGWNCentral.Core.Domain.Entities;

public sealed class MeteringPoint : SoftDeletableEntity
{
    public Guid CustomerId { get; set; }

    public string MeteringPointCode { get; set; } = string.Empty;

    public string EnergyDirection { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Customer Customer { get; set; } = null!;
}
