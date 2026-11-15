using EGWNCentral.Api.Contracts.InternalRegistration;
using EGWNCentral.Api.Controllers;
using EGWNCentral.Api.Options;
using EGWNCentral.Api.Services;
using EGWNCentral.Core.Domain.Entities;
using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EGWNCentral.Tests.Infrastructure;

public sealed class InternalRegistrationControllerTests
{
    [Fact]
    public async Task ResolveAssignAndMembership_ShouldUseStableCustomerOrganization()
    {
        await using var dbContext = CreateDbContext();
        var customer = await AddCustomerAsync(dbContext, "ABC123");
        var controller = CreateController(dbContext);

        var resolved = Assert.IsType<ResolvedRegistrationResponse>(
            Assert.IsType<OkObjectResult>((await controller.Resolve(new ResolveRegistrationRequest("ABC123"), default)).Result).Value);
        Assert.Equal($"egwn-{customer.Id:N}", resolved.Organization);

        var assignment = new AssignRegistrationRequest(resolved.Ticket, "keycloak-user-1");
        var assigned = Assert.IsType<MembershipResponse>(
            Assert.IsType<OkObjectResult>((await controller.Assign(assignment, default)).Result).Value);
        Assert.Equal(customer.Id, assigned.CustomerId);
        Assert.Equal(assigned, Assert.IsType<MembershipResponse>(
            Assert.IsType<OkObjectResult>((await controller.Assign(assignment, default)).Result).Value));

        var membership = Assert.IsType<MembershipResponse>(
            Assert.IsType<OkObjectResult>((await controller.Membership(new MembershipRequest("keycloak-user-1"), default)).Result).Value);
        Assert.Equal(assigned, membership);
    }

    [Fact]
    public async Task Assign_ShouldRejectExpiredCodeGenerationAndReassignment()
    {
        await using var dbContext = CreateDbContext();
        var firstCustomer = await AddCustomerAsync(dbContext, "ABC123");
        var secondCustomer = await AddCustomerAsync(dbContext, "DEF456");
        var controller = CreateController(dbContext);
        var firstTicket = await ResolveTicketAsync(controller, "ABC123");
        var secondTicket = await ResolveTicketAsync(controller, "DEF456");

        Assert.IsType<OkObjectResult>((await controller.Assign(
            new AssignRegistrationRequest(firstTicket, "keycloak-user-1"), default)).Result);
        Assert.IsType<BadRequestResult>((await controller.Assign(
            new AssignRegistrationRequest(secondTicket, "keycloak-user-1"), default)).Result);

        await new CustomerService(new UnitOfWork(dbContext)).RotateRegistrationCodeAsync(firstCustomer.Id);
        Assert.IsType<BadRequestResult>((await controller.Assign(
            new AssignRegistrationRequest(firstTicket, "keycloak-user-2"), default)).Result);
        Assert.NotEqual(firstCustomer.Id, secondCustomer.Id);
    }

    [Fact]
    public async Task Membership_ShouldRejectDeletedCustomers()
    {
        await using var dbContext = CreateDbContext();
        var customer = await AddCustomerAsync(dbContext, "ABC123");
        var controller = CreateController(dbContext);
        var ticket = await ResolveTicketAsync(controller, "ABC123");
        Assert.IsType<OkObjectResult>((await controller.Assign(
            new AssignRegistrationRequest(ticket, "keycloak-user-1"), default)).Result);

        customer.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        Assert.IsType<BadRequestResult>((await controller.Membership(
            new MembershipRequest("keycloak-user-1"), default)).Result);
    }

    private static InternalRegistrationController CreateController(ApplicationDbContext dbContext)
    {
        var options = Options.Create(new InternalRegistrationOptions
        {
            Token = new string('t', 32),
            TicketSigningKey = new string('s', 32)
        });
        return new InternalRegistrationController(dbContext, new RegistrationTicketService(options));
    }

    private static async Task<Customer> AddCustomerAsync(ApplicationDbContext dbContext, string code)
    {
        var customer = new Customer
        {
            CustomerNumber = $"K-{Guid.NewGuid():N}",
            Name = "Test",
            RegistrationCode = code
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();
        return customer;
    }

    private static async Task<string> ResolveTicketAsync(InternalRegistrationController controller, string code)
    {
        var result = await controller.Resolve(new ResolveRegistrationRequest(code), default);
        return Assert.IsType<ResolvedRegistrationResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value).Ticket;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"egwn-internal-registration-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
