using System.ComponentModel.DataAnnotations;

namespace EGWNCentral.Api.Contracts.EnergyCommunities;

public sealed class SetCommunityParticipationRequest
{
    [Range(typeof(decimal), "0", "100")]
    public decimal ParticipationPercentage { get; set; }
}
