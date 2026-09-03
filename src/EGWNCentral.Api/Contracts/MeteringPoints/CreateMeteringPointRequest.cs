using System.ComponentModel.DataAnnotations;

namespace EGWNCentral.Api.Contracts.MeteringPoints;

public sealed class CreateMeteringPointRequest
{
    public Guid CustomerId { get; set; }

    public string MeteringPointCode { get; set; } = string.Empty;

    [RegularExpression("^(generation|consumption)$", ErrorMessage = "Energy direction must be either 'generation' or 'consumption'.")]
    public string EnergyDirection { get; set; } = string.Empty;

    public string? Description { get; set; }
}
