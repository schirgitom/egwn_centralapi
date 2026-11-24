using System.Text.Json;
using EGWNCentral.Api.Contracts.Customers;
using EGWNCentral.Api.Controllers;
using EGWNCentral.Infrastructure.Messaging;
using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EGWNCentral.Tests.Infrastructure;

public sealed class CustomerSnapshotServiceTests
{
    [Fact]
    public async Task EnqueueSnapshot_ShouldPersistFullCreatedEventAndDispatchIt()
    {
        await using var context = CreateContext();
        var unitOfWork = new UnitOfWork(context);
        var customers = new CustomerService(unitOfWork);
        var meterService = new MeteringPointService(unitOfWork);
        var communityService = new EnergyCommunityService(unitOfWork);
        var customer = await customers.CreateAsync("232", "Customer", "customer@example.com");
        var other = await customers.CreateAsync("233", "Other", null);
        var meter = await meterService.CreateAsync([customer.Id, other.Id], "AT-SHARED", "generation", "Solar");
        var otherMeter = await meterService.CreateAsync([other.Id], "AT-OTHER", "consumption", null);
        var community = await communityService.CreateAsync("Community", "EDA", "RC1");
        await communityService.SetParticipationAsync(community.Id, meter.Id, 75);
        await communityService.SetParticipationAsync(community.Id, otherMeter.Id, 50);
        context.OutboxMessages.RemoveRange(context.OutboxMessages);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new CustomersController(customers, new CustomerSnapshotService(context));
        var result = await controller.EnqueueSnapshot(" 232 ", CancellationToken.None);
        var accepted = Assert.IsType<AcceptedResult>(result.Result);
        var response = Assert.IsType<CustomerSnapshotResponse>(accepted.Value);
        context.ChangeTracker.Clear();
        var message = await context.OutboxMessages.SingleAsync();
        Assert.Equal(response.EventId, message.Id);
        Assert.NotEqual(customer.Id, message.Id);
        using var json = JsonDocument.Parse(message.Payload);
        var root = json.RootElement;
        Assert.Equal(message.Id, root.GetProperty("eventId").GetGuid());
        Assert.Equal("customer.created", root.GetProperty("eventType").GetString());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(message.OccurredAtUtc, root.GetProperty("occurredAtUtc").GetDateTimeOffset());
        var data = root.GetProperty("customer");
        Assert.Equal(customer.Id, data.GetProperty("id").GetGuid());
        Assert.Equal("232", data.GetProperty("customerNumber").GetString());
        Assert.Equal(customer.RegistrationCode, data.GetProperty("registrationCode").GetString());
        Assert.Equal("customer@example.com", data.GetProperty("email").GetString());
        Assert.False(data.GetProperty("isDeleted").GetBoolean());
        var point = Assert.Single(data.GetProperty("meteringPoints").EnumerateArray());
        Assert.Equal(meter.Id, point.GetProperty("id").GetGuid());
        Assert.Equal("generation", point.GetProperty("energyDirection").GetString());
        var group = Assert.Single(data.GetProperty("energyCommunities").EnumerateArray());
        Assert.Equal(community.Id, group.GetProperty("id").GetGuid());
        Assert.Equal("RC1", group.GetProperty("sourceId").GetString());
        var participation = Assert.Single(group.GetProperty("meteringPoints").EnumerateArray());
        Assert.Equal(meter.Id, participation.GetProperty("id").GetGuid());
        Assert.Equal(75, participation.GetProperty("participationPercentage").GetDecimal());

        var publisher = new RecordingPublisher();
        var dispatcher = new CustomerOutboxDispatcher(context, publisher, Options.Create(new RabbitMqOptions()));
        await dispatcher.DispatchAsync(CancellationToken.None);
        Assert.Equal(message.Payload, Assert.Single(publisher.Messages).Payload);
        Assert.Empty(await context.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task EnqueueSnapshot_ShouldUseNewIdsAndRejectUnknownOrDeletedCustomers()
    {
        await using var context = CreateContext();
        var customers = new CustomerService(new UnitOfWork(context));
        var customer = await customers.CreateAsync("232", "Customer", null);
        var controller = new CustomersController(customers, new CustomerSnapshotService(context));

        var first = Assert.IsType<CustomerSnapshotResponse>(
            Assert.IsType<AcceptedResult>((await controller.EnqueueSnapshot("232", CancellationToken.None)).Result).Value);
        var second = Assert.IsType<CustomerSnapshotResponse>(
            Assert.IsType<AcceptedResult>((await controller.EnqueueSnapshot("232", CancellationToken.None)).Result).Value);
        Assert.NotEqual(first.EventId, second.EventId);
        Assert.IsType<NotFoundResult>((await controller.EnqueueSnapshot("missing", CancellationToken.None)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.EnqueueSnapshot(" ", CancellationToken.None)).Result);

        await customers.DeleteAsync(customer.Id);
        var count = await context.OutboxMessages.CountAsync();
        context.ChangeTracker.Clear();
        Assert.IsType<NotFoundResult>((await controller.EnqueueSnapshot("232", CancellationToken.None)).Result);
        Assert.Equal(count, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task EnqueueAllSnapshots_ShouldCreateFullEventsForActiveCustomersOnly()
    {
        await using var context = CreateContext();
        var unit = new UnitOfWork(context);
        var customers = new CustomerService(unit);
        var meterService = new MeteringPointService(unit);
        var communities = new EnergyCommunityService(unit);
        var first = await customers.CreateAsync("101", "First", "first@example.com");
        var second = await customers.CreateAsync("102", "Second", null);
        var deleted = await customers.CreateAsync("103", "Deleted", null);
        var shared = await meterService.CreateAsync([first.Id, second.Id], "AT-SHARED", "generation", null);
        var own = await meterService.CreateAsync([first.Id], "AT-OWN", "consumption", null);
        var community = await communities.CreateAsync("Community", "EDA", "RC1");
        await communities.SetParticipationAsync(community.Id, shared.Id, 75);
        await communities.SetParticipationAsync(community.Id, own.Id, 25);
        await customers.DeleteAsync(deleted.Id);
        context.OutboxMessages.RemoveRange(context.OutboxMessages);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new CustomersController(customers, new CustomerSnapshotService(context));
        var response = Assert.IsType<CustomerSnapshotsResponse>(
            Assert.IsType<AcceptedResult>((await controller.EnqueueAllSnapshots(CancellationToken.None)).Result).Value);
        Assert.Equal(2, response.Count);
        var messages = await context.OutboxMessages.ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Equal(2, messages.Select(message => message.Id).Distinct().Count());
        var byCustomer = messages.Select(message =>
        {
            using var json = JsonDocument.Parse(message.Payload);
            var root = json.RootElement;
            Assert.Equal("customer.created", root.GetProperty("eventType").GetString());
            Assert.Equal(message.Id, root.GetProperty("eventId").GetGuid());
            return root.GetProperty("customer").Clone();
        }).ToDictionary(data => data.GetProperty("id").GetGuid());
        Assert.Equal(first.RegistrationCode, byCustomer[first.Id].GetProperty("registrationCode").GetString());
        Assert.Equal(second.RegistrationCode, byCustomer[second.Id].GetProperty("registrationCode").GetString());
        Assert.Equal(2, byCustomer[first.Id].GetProperty("meteringPoints").GetArrayLength());
        Assert.Equal(shared.Id, Assert.Single(byCustomer[second.Id].GetProperty("meteringPoints").EnumerateArray()).GetProperty("id").GetGuid());
        var secondCommunity = Assert.Single(byCustomer[second.Id].GetProperty("energyCommunities").EnumerateArray());
        Assert.Equal(shared.Id, Assert.Single(secondCommunity.GetProperty("meteringPoints").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(75, secondCommunity.GetProperty("meteringPoints")[0].GetProperty("participationPercentage").GetDecimal());
        Assert.DoesNotContain(deleted.Id, byCustomer.Keys);

        context.ChangeTracker.Clear();
        var repeat = Assert.IsType<CustomerSnapshotsResponse>(
            Assert.IsType<AcceptedResult>((await controller.EnqueueAllSnapshots(CancellationToken.None)).Result).Value);
        Assert.Equal(2, repeat.Count);
        Assert.Equal(4, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task EnqueueAllSnapshots_WithNoActiveCustomersReturnsZero()
    {
        await using var context = CreateContext();
        var customers = new CustomerService(new UnitOfWork(context));
        var customer = await customers.CreateAsync("101", "First", null);
        await customers.DeleteAsync(customer.Id);
        context.OutboxMessages.RemoveRange(context.OutboxMessages);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new CustomersController(customers, new CustomerSnapshotService(context));
        var result = await controller.EnqueueAllSnapshots(CancellationToken.None);
        Assert.Equal(0, Assert.IsType<CustomerSnapshotsResponse>(Assert.IsType<AcceptedResult>(result.Result).Value).Count);
        Assert.Empty(await context.OutboxMessages.ToListAsync());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"customer-snapshot-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class RecordingPublisher : ICustomerEventPublisher
    {
        public List<OutboxMessage> Messages { get; } = [];

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
