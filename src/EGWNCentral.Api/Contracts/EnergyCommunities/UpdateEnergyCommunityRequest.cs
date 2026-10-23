using System.ComponentModel.DataAnnotations;

namespace EGWNCentral.Api.Contracts.EnergyCommunities;

public sealed class UpdateEnergyCommunityRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Source { get; set; }
}
