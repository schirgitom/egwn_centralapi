using System.Text.Json;
using EGWNCentral.Core.Domain.Entities;
using EGWNCentral.Infrastructure.Messaging;
using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Persistence.Migrations;
using EGWNCentral.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Options;

namespace EGWNCentral.Tests.Infrastructure;

public sealed class CustomerOutboxTests
{
    [Fact]
    public void RegistrationCodeGenerator_ShouldAlwaysUseSixUnambiguousCharacters()
    {
        var codes = Enumerable.Range(0, 10000).Select(_ => RegistrationCodeGenerator.Generate()).ToArray();
        Assert.All(codes, code => Assert.Matches("^[A-NP-Z1-9]{6}$", code));
        Assert.All(codes, code => Assert.True(RegistrationCodeGenerator.IsValid(code)));
    }

    [Theory]
    [InlineData("ABO123")]
    [InlineData("ABC120")]
    [InlineData("abc123")]
    [InlineData("ABC12")]
    [InlineData("ABC1234")]
    public async Task RegistrationCode_ShouldRejectInvalidExplicitValues(string code)
    {
        await using var context = CreateContext();
        context.Customers.Add(new Customer { CustomerNumber = "INVALID", Name = "Invalid", RegistrationCode = code });
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public void Migration_ShouldOnlyAddOutboxTableAndIndex()
    {
        var migration = new AddCustomerCreatedOutbox();
        Assert.Equal(2, migration.UpOperations.Count);
        var table = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
        Assert.Equal("CustomerCreatedOutbox", table.Name);
        Assert.Equal(["Id", "OccurredAtUtc", "Payload"], table.Columns.Select(x => x.Name));
        Assert.Equal("CustomerCreatedOutbox", Assert.Single(migration.UpOperations.OfType<CreateIndexOperation>()).Table);
        Assert.Equal("CustomerCreatedOutbox", Assert.IsType<DropTableOperation>(Assert.Single(migration.DownOperations)).Name);
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistNormalizedVersionedEventWithCustomer()
    {
        await using var context = CreateContext();
        var service = new CustomerService(new UnitOfWork(context));

        var customer = await service.CreateAsync(" K-100 ", " Test Customer ", " test@example.com ");
        context.ChangeTracker.Clear();

        var message = await context.OutboxMessages.SingleAsync();
        using var json = JsonDocument.Parse(message.Payload);
        var root = json.RootElement;
        Assert.Equal(customer.Id, root.GetProperty("eventId").GetGuid());
        Assert.Equal(message.Id, root.GetProperty("eventId").GetGuid());
        Assert.Equal("customer.created", root.GetProperty("eventType").GetString());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(customer.CreatedAtUtc, root.GetProperty("occurredAtUtc").GetDateTimeOffset());
        Assert.Equal(customer.CreatedAtUtc, message.OccurredAtUtc);
        var data = root.GetProperty("customer");
        Assert.Equal(customer.Id, data.GetProperty("id").GetGuid());
        Assert.Equal("K-100", data.GetProperty("customerNumber").GetString());
        Assert.Matches("^[A-NP-Z1-9]{6}$", customer.RegistrationCode);
        Assert.Equal(customer.RegistrationCode, data.GetProperty("registrationCode").GetString());
        Assert.Equal(customer.RegistrationCode, (await context.Customers.SingleAsync()).RegistrationCode);
        Assert.Equal("Test Customer", data.GetProperty("name").GetString());
        Assert.Equal("test@example.com", data.GetProperty("email").GetString());
        Assert.Empty(data.GetProperty("meteringPoints").EnumerateArray());
        Assert.Empty(data.GetProperty("energyCommunities").EnumerateArray());
    }

    [Fact]
    public async Task UpdateAndDelete_ShouldEnqueueNewSnapshotsButRejectedCreatesShouldNot()
    {
        await using var context = CreateContext();
        var service = new CustomerService(new UnitOfWork(context));
        var customer = await service.CreateAsync("K-100", "Original", null);
        var payload = (await context.OutboxMessages.SingleAsync()).Payload;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync("K-100", "Duplicate", null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync("K-200", " ", null));
        Assert.Single(await context.OutboxMessages.ToListAsync());
        await service.UpdateAsync(customer.Id, "Changed", "changed@example.com");
        await service.DeleteAsync(customer.Id);

        var messages = await context.OutboxMessages.ToListAsync();
        Assert.Equal(3, messages.Count);
        Assert.Equal(3, messages.Select(message => message.Id).Distinct().Count());
        var deleted = Assert.Single(messages.Select(ReadSnapshot), snapshot => snapshot.EventType == "customer.deleted");
        Assert.True(deleted.Customer.IsDeleted);
        Assert.Empty(deleted.Customer.MeteringPoints);
        Assert.Empty(deleted.Customer.EnergyCommunities);
        Assert.Equal("Changed", deleted.Customer.Name);
        Assert.Equal(customer.RegistrationCode, deleted.Customer.RegistrationCode);
        Assert.Contains(messages, message => message.Payload == payload);
        using var json = JsonDocument.Parse(payload);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("customer").GetProperty("email").ValueKind);
    }

    [Fact]
    public void SynchronousSave_ShouldAlsoEnqueueOnlyOnce()
    {
        using var context = CreateContext();
        context.Customers.Add(new Customer { CustomerNumber = "K-SYNC", Name = "Synchronous" });
        context.SaveChanges();
        context.SaveChanges();

        Assert.Single(context.OutboxMessages);
    }

    [Fact]
    public async Task FailedSaveRetry_ShouldKeepIdentityAndTimeButUseCurrentMasterData()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"outbox-retry-{Guid.NewGuid()}")
            .AddInterceptors(new FailFirstSaveInterceptor())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var customer = new Customer { CustomerNumber = "K-RETRY", Name = "Original" };
        context.Customers.Add(customer);
        await Assert.ThrowsAsync<IOException>(() => context.SaveChangesAsync());
        var pending = Assert.Single(context.OutboxMessages.Local);
        var eventId = pending.Id;
        var occurredAt = pending.OccurredAtUtc;
        customer.Name = "Corrected";

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var saved = await context.OutboxMessages.SingleAsync();
        Assert.Equal(eventId, saved.Id);
        Assert.Equal(occurredAt, saved.OccurredAtUtc);
        Assert.Equal(occurredAt, (await context.Customers.SingleAsync()).CreatedAtUtc);
        using var json = JsonDocument.Parse(saved.Payload);
        Assert.Equal("Corrected", json.RootElement.GetProperty("customer").GetProperty("name").GetString());
        Assert.Equal(customer.RegistrationCode, json.RootElement.GetProperty("customer").GetProperty("registrationCode").GetString());
    }

    [Fact]
    public async Task Dispatch_ShouldRemoveOnlyConfirmedEventsAndRetryUnchangedPayload()
    {
        await using var context = CreateContext();
        var service = new CustomerService(new UnitOfWork(context));
        await service.CreateAsync("K-1", "First", null);
        await service.CreateAsync("K-2", "Second", null);
        var messages = await context.OutboxMessages.OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id).ToListAsync();
        var publisher = new RecordingPublisher { FailOnCall = 2 };
        var dispatcher = new CustomerOutboxDispatcher(context, publisher, Options.Create(new RabbitMqOptions()));

        await Assert.ThrowsAsync<IOException>(() => dispatcher.DispatchAsync(CancellationToken.None));
        context.ChangeTracker.Clear();
        Assert.Equal(messages[1].Id, (await context.OutboxMessages.SingleAsync()).Id);

        publisher.FailOnCall = null;
        await dispatcher.DispatchAsync(CancellationToken.None);
        Assert.Empty(await context.OutboxMessages.ToListAsync());
        Assert.Equal(messages[1].Id, publisher.Published[^1].Id);
        Assert.Equal(messages[1].Payload, publisher.Published[^1].Payload);
    }

    [Fact]
    public async Task Dispatch_ShouldRespectBatchSize()
    {
        await using var context = CreateContext();
        var service = new CustomerService(new UnitOfWork(context));
        await service.CreateAsync("K-1", "First", null);
        await service.CreateAsync("K-2", "Second", null);
        var publisher = new RecordingPublisher();
        var dispatcher = new CustomerOutboxDispatcher(context, publisher,
            Options.Create(new RabbitMqOptions { BatchSize = 1 }));

        await dispatcher.DispatchAsync(CancellationToken.None);

        Assert.Single(publisher.Published);
        Assert.Single(await context.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task DispatchCancellation_ShouldKeepEventForRetry()
    {
        await using var context = CreateContext();
        await new CustomerService(new UnitOfWork(context)).CreateAsync("K-1", "First", null);
        var publisher = new RecordingPublisher { CancelPublish = true };
        var dispatcher = new CustomerOutboxDispatcher(context, publisher, Options.Create(new RabbitMqOptions()));

        await Assert.ThrowsAsync<OperationCanceledException>(() => dispatcher.DispatchAsync(CancellationToken.None));

        Assert.Single(await context.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task ParticipationChanges_ShouldIncludeAllCustomerMetersAndGroupCommunities()
    {
        await using var context = CreateContext();
        var unit = new UnitOfWork(context);
        var customer = await new CustomerService(unit).CreateAsync("K-1", "First", null);
        var meters = new MeteringPointService(unit);
        var firstMeter = await meters.CreateAsync([customer.Id], "AT-1", "consumption", "Home");
        var secondMeter = await meters.CreateAsync([customer.Id], "AT-2", "generation", null);
        var communities = new EnergyCommunityService(unit);
        var firstCommunity = await communities.CreateAsync("Community A", "EDA", "A");
        var secondCommunity = await communities.CreateAsync("Community B", "EEGFaktura", "B");
        await ClearOutbox(context);

        context.ChangeTracker.Clear();
        await communities.SetParticipationAsync(firstCommunity.Id, firstMeter.Id, 75);
        var snapshot = await SingleSnapshot(context);
        Assert.Equal("customer.updated", snapshot.EventType);
        Assert.Equal(2, snapshot.Customer.MeteringPoints.Count);
        var first = Assert.Single(snapshot.Customer.EnergyCommunities);
        Assert.Equal(firstCommunity.Id, first.Id);
        Assert.Equal("EDA", first.Source);
        Assert.Equal("A", first.SourceId);
        Assert.Equal("AT-1", Assert.Single(first.MeteringPoints).MeteringPointCode);
        Assert.Equal(75, Assert.Single(first.MeteringPoints).ParticipationPercentage);

        await ClearOutbox(context);
        context.ChangeTracker.Clear();
        await communities.SetParticipationAsync(secondCommunity.Id, firstMeter.Id, 20);
        await ClearOutbox(context);
        context.ChangeTracker.Clear();
        await communities.SetParticipationAsync(firstCommunity.Id, secondMeter.Id, 90);
        snapshot = await SingleSnapshot(context);
        Assert.Equal(2, snapshot.Customer.EnergyCommunities.Count);
        Assert.Equal(2, snapshot.Customer.EnergyCommunities.Single(x => x.Id == firstCommunity.Id).MeteringPoints.Count);

        await ClearOutbox(context);
        context.ChangeTracker.Clear();
        await communities.SetParticipationAsync(firstCommunity.Id, firstMeter.Id, 50);
        snapshot = await SingleSnapshot(context);
        Assert.Equal(50, snapshot.Customer.EnergyCommunities.Single(x => x.Id == firstCommunity.Id)
            .MeteringPoints.Single(x => x.Id == firstMeter.Id).ParticipationPercentage);

        await ClearOutbox(context);
        context.ChangeTracker.Clear();
        await communities.RemoveParticipationAsync(firstCommunity.Id, secondMeter.Id);
        snapshot = await SingleSnapshot(context);
        Assert.Equal(firstMeter.Id, Assert.Single(snapshot.Customer.EnergyCommunities.Single(x => x.Id == firstCommunity.Id)
            .MeteringPoints).Id);
        Assert.Equal(2, snapshot.Customer.MeteringPoints.Count);
    }

    [Fact]
    public async Task ReassigningSharedMeter_ShouldNotifyOldAndNewCustomersWithoutLeakingOtherMeters()
    {
        await using var context = CreateContext();
        var unit = new UnitOfWork(context);
        var customers = new CustomerService(unit);
        var first = await customers.CreateAsync("K-1", "First", null);
        var second = await customers.CreateAsync("K-2", "Second", null);
        var third = await customers.CreateAsync("K-3", "Third", null);
        var meters = new MeteringPointService(unit);
        var shared = await meters.CreateAsync([first.Id, second.Id], "AT-SHARED", "consumption", null);
        await meters.CreateAsync([second.Id], "AT-PRIVATE", "generation", null);
        var communities = new EnergyCommunityService(unit);
        var community = await communities.CreateAsync("Shared");
        await communities.SetParticipationAsync(community.Id, shared.Id, 80);
        await ClearOutbox(context);
        context.ChangeTracker.Clear();

        await meters.UpdateAsync(shared.Id, null, null, [second.Id, third.Id]);

        var snapshots = (await context.OutboxMessages.ToListAsync()).Select(ReadSnapshot).ToList();
        var oldCustomer = Assert.Single(snapshots, x => x.Customer.Id == first.Id);
        Assert.Empty(oldCustomer.Customer.MeteringPoints);
        Assert.Empty(oldCustomer.Customer.EnergyCommunities);
        var newCustomer = Assert.Single(snapshots, x => x.Customer.Id == third.Id);
        Assert.Equal(shared.Id, Assert.Single(newCustomer.Customer.MeteringPoints).Id);
        Assert.Single(newCustomer.Customer.EnergyCommunities);
        Assert.All(snapshots.Where(x => x.Customer.Id != second.Id),
            x => Assert.DoesNotContain(x.Customer.MeteringPoints, meter => meter.MeteringPointCode == "AT-PRIVATE"));
        context.ChangeTracker.Clear();
        var persisted = await meters.GetByIdAsync(shared.Id);
        Assert.Equal(new[] { second.Id, third.Id }.Order(), persisted!.Customers.Select(x => x.Id).Order());
    }

    [Fact]
    public async Task CommunityAndMeterChangesAndDeletion_ShouldRefreshAffectedCustomersOnly()
    {
        await using var context = CreateContext();
        var unit = new UnitOfWork(context);
        var customers = new CustomerService(unit);
        var customer = await customers.CreateAsync("K-1", "First", null);
        await customers.CreateAsync("K-OTHER", "Unrelated", null);
        var meters = new MeteringPointService(unit);
        var meter = await meters.CreateAsync([customer.Id], "AT-1", "consumption", null);
        var communities = new EnergyCommunityService(unit);
        var community = await communities.CreateAsync("Original");
        await communities.SetParticipationAsync(community.Id, meter.Id, 70);
        await ClearOutbox(context);
        context.ChangeTracker.Clear();

        await communities.UpdateAsync(community.Id, "Renamed", "EDA", "external");
        var snapshot = await SingleSnapshot(context);
        Assert.Equal(customer.Id, snapshot.Customer.Id);
        Assert.Equal("Renamed", Assert.Single(snapshot.Customer.EnergyCommunities).Name);

        await ClearOutbox(context);
        context.ChangeTracker.Clear();
        await meters.UpdateAsync(meter.Id, "New description", "generation");
        snapshot = await SingleSnapshot(context);
        Assert.Equal("generation", Assert.Single(snapshot.Customer.MeteringPoints).EnergyDirection);
        Assert.Equal("New description", Assert.Single(Assert.Single(snapshot.Customer.EnergyCommunities).MeteringPoints).Description);

        await ClearOutbox(context);
        context.ChangeTracker.Clear();
        await communities.DeleteAsync(community.Id);
        snapshot = await SingleSnapshot(context);
        Assert.Empty(snapshot.Customer.EnergyCommunities);
        Assert.Single(snapshot.Customer.MeteringPoints);

        await ClearOutbox(context);
        context.ChangeTracker.Clear();
        await meters.DeleteAsync(meter.Id);
        snapshot = await SingleSnapshot(context);
        Assert.Empty(snapshot.Customer.MeteringPoints);
        Assert.Empty(snapshot.Customer.EnergyCommunities);
    }

    [Fact]
    public async Task SingleSaveGraphCreation_ShouldCaptureNewRelationsBeforePersistence()
    {
        await using var context = CreateContext();
        var customer = new Customer { CustomerNumber = "K-GRAPH", Name = "Graph" };
        var meter = new MeteringPoint
        {
            MeteringPointCode = "AT-GRAPH", EnergyDirection = "consumption", Customers = [customer]
        };
        var community = new EnergyCommunity { Name = "Graph community" };
        context.CommunityParticipations.Add(new CommunityParticipation
        {
            MeteringPoint = meter, EnergyCommunity = community, ParticipationPercentage = 35
        });

        await context.SaveChangesAsync();

        var snapshot = await SingleSnapshot(context);
        Assert.Equal("customer.created", snapshot.EventType);
        Assert.Equal(meter.Id, Assert.Single(snapshot.Customer.MeteringPoints).Id);
        Assert.Equal(community.Id, Assert.Single(snapshot.Customer.EnergyCommunities).Id);
        Assert.Equal(35, Assert.Single(Assert.Single(snapshot.Customer.EnergyCommunities).MeteringPoints).ParticipationPercentage);
    }

    [Fact]
    public async Task UpdatingCustomer_ShouldIncludePreviouslySavedRelations()
    {
        await using var context = CreateContext();
        var unit = new UnitOfWork(context);
        var customerService = new CustomerService(unit);
        var customer = await customerService.CreateAsync("K-1", "Original", null);
        var meter = await new MeteringPointService(unit).CreateAsync([customer.Id], "AT-1", "consumption", null);
        var communities = new EnergyCommunityService(unit);
        var community = await communities.CreateAsync("Community");
        await communities.SetParticipationAsync(community.Id, meter.Id, 80);
        await ClearOutbox(context);
        context.ChangeTracker.Clear();

        await customerService.UpdateAsync(customer.Id, "Changed", "changed@example.com");

        var snapshot = await SingleSnapshot(context);
        Assert.Equal("Changed", snapshot.Customer.Name);
        Assert.Single(snapshot.Customer.MeteringPoints);
        Assert.Single(snapshot.Customer.EnergyCommunities);
        Assert.Equal(customer.RegistrationCode, snapshot.Customer.RegistrationCode);
    }

    [Fact]
    public async Task RegistrationCodes_ShouldBeDistinctAndRejectChanges()
    {
        await using var context = CreateContext();
        var service = new CustomerService(new UnitOfWork(context));
        var first = await service.CreateAsync("K-1", "First", null);
        var second = await service.CreateAsync("K-2", "Second", null);
        Assert.NotEqual(first.RegistrationCode, second.RegistrationCode);
        Assert.Matches("^[A-NP-Z1-9]{6}$", second.RegistrationCode);
        context.ChangeTracker.Clear();
        var tracked = await context.Customers.SingleAsync(customer => customer.Id == first.Id);
        tracked.RegistrationCode = "ABC123";

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

        context.ChangeTracker.Clear();
        Assert.Equal(first.RegistrationCode,
            (await context.Customers.SingleAsync(customer => customer.Id == first.Id)).RegistrationCode);
    }

    private static CustomerSnapshot ReadSnapshot(OutboxMessage message)
        => JsonSerializer.Deserialize<CustomerSnapshot>(message.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static async Task<CustomerSnapshot> SingleSnapshot(ApplicationDbContext context)
        => ReadSnapshot(await context.OutboxMessages.SingleAsync());

    private static async Task ClearOutbox(ApplicationDbContext context)
    {
        context.OutboxMessages.RemoveRange(await context.OutboxMessages.ToListAsync());
        await context.SaveChangesAsync();
    }

    private static ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"outbox-{Guid.NewGuid()}").Options);

    private sealed class RecordingPublisher : ICustomerEventPublisher
    {
        public List<OutboxMessage> Published { get; } = [];
        public int? FailOnCall { get; set; }
        public bool CancelPublish { get; set; }
        private int _calls;

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            if (CancelPublish)
            {
                throw new OperationCanceledException();
            }
            if (++_calls == FailOnCall)
            {
                throw new IOException("Broker unavailable.");
            }
            Published.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FailFirstSaveInterceptor : SaveChangesInterceptor
    {
        private bool _failed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_failed)
            {
                _failed = true;
                throw new IOException("Simulated persistence failure.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
