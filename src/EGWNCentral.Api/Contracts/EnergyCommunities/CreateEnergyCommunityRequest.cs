using System.ComponentModel.DataAnnotations;

namespace EGWNCentral.Api.Contracts.EnergyCommunities;

public sealed class CreateEnergyCommunityRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Source { get; set; }
}
