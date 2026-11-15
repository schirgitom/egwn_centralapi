using EGWNCentral.Api.Contracts.InternalRegistration;
using EGWNCentral.Api.Services;
using EGWNCentral.Core.Domain.Entities;
using EGWNCentral.Infrastructure.Messaging;
using EGWNCentral.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EGWNCentral.Api.Controllers;

[ApiController]
[Route("internal/registration")]
[Authorize(AuthenticationSchemes = InternalBearerDefaults.AuthenticationScheme)]
public sealed class InternalRegistrationController(
    ApplicationDbContext dbContext,
    RegistrationTicketService ticketService) : ControllerBase
{
    [HttpPost("resolve")]
    public async Task<ActionResult<ResolvedRegistrationResponse>> Resolve(
        [FromBody] ResolveRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Code is not { Length: <= 512 } code || !RegistrationCodeGenerator.IsValid(code))
        {
            return BadRequest();
        }

        var customer = await dbContext.Customers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RegistrationCode == code, cancellationToken);
        if (customer is null)
        {
            return BadRequest();
        }

        var ticket = ticketService.Create(customer.Id, customer.RegistrationCode);
        return Ok(new ResolvedRegistrationResponse(ticket, OrganizationFor(customer.Id)));
    }

    [HttpPost("assign")]
    public async Task<ActionResult<MembershipResponse>> Assign(
        [FromBody] AssignRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.UserId is not { Length: <= 255 } userId ||
            string.IsNullOrWhiteSpace(userId) ||
            !ticketService.TryValidate(request.Ticket, out var customerId, out var registrationCodeHash))
        {
            return BadRequest();
        }

        var customer = await dbContext.Customers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        if (customer is null ||
            !string.Equals(registrationCodeHash,
                RegistrationTicketService.HashRegistrationCode(customer.RegistrationCode), StringComparison.Ordinal))
        {
            return BadRequest();
        }

        var existing = await dbContext.KeycloakMemberships.AsNoTracking()
            .SingleOrDefaultAsync(x => x.KeycloakUserId == userId, cancellationToken);
        if (existing is not null)
        {
            return existing.CustomerId == customer.Id ? Ok(ToResponse(customer.Id)) : BadRequest();
        }

        var membership = new KeycloakMembership
        {
            KeycloakUserId = userId,
            CustomerId = customer.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.KeycloakMemberships.Add(membership);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_KeycloakMemberships" })
        {
            dbContext.Entry(membership).State = EntityState.Detached;
            var concurrent = await dbContext.KeycloakMemberships.AsNoTracking()
                .SingleOrDefaultAsync(x => x.KeycloakUserId == userId, cancellationToken);
            return concurrent?.CustomerId == customer.Id ? Ok(ToResponse(customer.Id)) : BadRequest();
        }

        return Ok(ToResponse(customer.Id));
    }

    [HttpPost("membership")]
    public async Task<ActionResult<MembershipResponse>> Membership(
        [FromBody] MembershipRequest request,
        CancellationToken cancellationToken)
    {
        if (request.UserId is not { Length: <= 255 } userId || string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest();
        }

        var customerId = await dbContext.KeycloakMemberships.AsNoTracking()
            .Where(x => x.KeycloakUserId == userId)
            .Select(x => (Guid?)x.CustomerId)
            .SingleOrDefaultAsync(cancellationToken);
        if (customerId is null ||
            !await dbContext.Customers.AsNoTracking().AnyAsync(x => x.Id == customerId, cancellationToken))
        {
            return BadRequest();
        }

        return Ok(ToResponse(customerId.Value));
    }

    private static MembershipResponse ToResponse(Guid customerId)
        => new(customerId, OrganizationFor(customerId));

    private static string OrganizationFor(Guid customerId)
        => $"egwn-{customerId:N}";
}
