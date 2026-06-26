namespace EGWNCentral.Api.Contracts.MeteringPoints;

public sealed class CreateMeteringPointRequest
{
    public Guid CustomerId { get; set; }

    public string MeteringPointCode { get; set; } = string.Empty;

    public string? Description { get; set; }
}
