using System.ComponentModel.DataAnnotations;

namespace EGWNCentral.Api.Contracts.MeteringPoints;

public sealed class UpdateMeteringPointRequest
{
    [MinLength(1)]
    public Guid[]? CustomerIds { get; set; }

    public string? Description { get; set; }

    [RegularExpression("^(generation|consumption)$", ErrorMessage = "Energy direction must be either 'generation' or 'consumption'.")]
    public string? EnergyDirection { get; set; }
}
