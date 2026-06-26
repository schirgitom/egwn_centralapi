using EGWNCentral.Api.Contracts.MeteringPoints;
using EGWNCentral.Core.Abstractions;
using EGWNCentral.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EGWNCentral.Api.Controllers;

[ApiController]
[Route("api/metering-points")]
[Authorize]
public sealed class MeteringPointsController(IMeteringPointService meteringPointService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<MeteringPointResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var meteringPoints = await meteringPointService.GetAllAsync(cancellationToken);
        return Ok(meteringPoints.Select(Map).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MeteringPointResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var meteringPoint = await meteringPointService.GetByIdAsync(id, cancellationToken);
        return meteringPoint is null ? NotFound() : Ok(Map(meteringPoint));
    }

    [HttpPost]
    public async Task<ActionResult<MeteringPointResponse>> Create([FromBody] CreateMeteringPointRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var meteringPoint = await meteringPointService.CreateAsync(request.CustomerId, request.MeteringPointCode, request.Description, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = meteringPoint.Id }, Map(meteringPoint));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MeteringPointResponse>> Update(Guid id, [FromBody] UpdateMeteringPointRequest request, CancellationToken cancellationToken)
    {
        var meteringPoint = await meteringPointService.UpdateAsync(id, request.Description, cancellationToken);
        return meteringPoint is null ? NotFound() : Ok(Map(meteringPoint));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await meteringPointService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private static MeteringPointResponse Map(MeteringPoint meteringPoint)
    {
        return new MeteringPointResponse(
            meteringPoint.Id,
            meteringPoint.CustomerId,
            meteringPoint.MeteringPointCode,
            meteringPoint.Description,
            meteringPoint.CreatedAtUtc,
            meteringPoint.UpdatedAtUtc);
    }
}
