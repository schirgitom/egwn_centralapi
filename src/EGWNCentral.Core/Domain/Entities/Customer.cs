namespace EGWNCentral.Core.Domain.Entities;

public sealed class Customer : SoftDeletableEntity
{
    public string CustomerNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public ICollection<MeteringPoint> MeteringPoints { get; set; } = new List<MeteringPoint>();
}
