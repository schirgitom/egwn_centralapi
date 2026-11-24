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

    [HttpGet("by-source")]
    public async Task<ActionResult<IReadOnlyCollection<EnergyCommunityResponse>>> GetEnergyCommunitiesPerSource(
        [FromQuery] string? source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return BadRequest("Source cannot be empty.");
        }

        var communities = await energyCommunityService.GetBySourceAsync(source, cancellationToken);
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
            var community = await energyCommunityService.CreateAsync(request.Name, request.Source, request.SourceId, cancellationToken);
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
            var community = await energyCommunityService.UpdateAsync(id, request.Name, request.Source, request.SourceId, cancellationToken);
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
            return Ok(MapParticipation(participation));
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
            community.Source,
            community.SourceId,
            community.Participations
                .Where(x => x.MeteringPoint is not null)
                .Select(MapParticipation)
                .ToArray(),
            community.CreatedAtUtc,
            community.UpdatedAtUtc);
    }

    private static CommunityParticipationResponse MapParticipation(CommunityParticipation participation)
    {
        return new CommunityParticipationResponse(
            participation.MeteringPointId,
            participation.MeteringPoint.MeteringPointCode,
            participation.MeteringPoint.EnergyDirection,
            participation.MeteringPoint.Customers
                .OrderBy(x => x.CustomerNumber)
                .ThenBy(x => x.Id)
                .Select(x => new ParticipationCustomerResponse(x.Id, x.CustomerNumber, x.Name))
                .ToArray(),
            participation.ParticipationPercentage);
    }
}
