using EGWNCentral.Api.Contracts.EnergyCommunities;
using EGWNCentral.Core.Abstractions;
using EGWNCentral.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EGWNCentral.Api.Controllers;

[ApiController]
[Route("api/energy-communities")]
[Authorize]
public sealed class EnergyCommunitiesController(IEnergyCommunityService energyCommunityService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EnergyCommunityResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var communities = await energyCommunityService.GetAllAsync(cancellationToken);
        return Ok(communities.Select(Map).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EnergyCommunityResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var community = await energyCommunityService.GetByIdAsync(id, cancellationToken);
        return community is null ? NotFound() : Ok(Map(community));
    }

    [HttpPost]
    public async Task<ActionResult<EnergyCommunityResponse>> Create(
        [FromBody] CreateEnergyCommunityRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var community = await energyCommunityService.CreateAsync(request.Name, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = community.Id }, Map(community));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EnergyCommunityResponse>> Update(
        Guid id,
        [FromBody] UpdateEnergyCommunityRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var community = await energyCommunityService.UpdateAsync(id, request.Name, cancellationToken);
            return community is null ? NotFound() : Ok(Map(community));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await energyCommunityService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPut("{communityId:guid}/metering-points/{meteringPointId:guid}")]
    public async Task<ActionResult<CommunityParticipationResponse>> SetParticipation(
        Guid communityId,
        Guid meteringPointId,
        [FromBody] SetCommunityParticipationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var participation = await energyCommunityService.SetParticipationAsync(
                communityId,
                meteringPointId,
                request.ParticipationPercentage,
                cancellationToken);
            var meteringPoint = participation.MeteringPoint;
            return Ok(new CommunityParticipationResponse(
                meteringPoint.Id,
                meteringPoint.MeteringPointCode,
                participation.ParticipationPercentage));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{communityId:guid}/metering-points/{meteringPointId:guid}")]
    public async Task<IActionResult> RemoveParticipation(
        Guid communityId,
        Guid meteringPointId,
        CancellationToken cancellationToken)
    {
        var removed = await energyCommunityService.RemoveParticipationAsync(communityId, meteringPointId, cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    private static EnergyCommunityResponse Map(EnergyCommunity community)
    {
        return new EnergyCommunityResponse(
            community.Id,
            community.Name,
            community.Participations
                .Where(x => x.MeteringPoint is not null)
                .Select(x => new CommunityParticipationResponse(
                    x.MeteringPointId,
                    x.MeteringPoint.MeteringPointCode,
                    x.ParticipationPercentage))
                .ToArray(),
            community.CreatedAtUtc,
            community.UpdatedAtUtc);
    }
}
