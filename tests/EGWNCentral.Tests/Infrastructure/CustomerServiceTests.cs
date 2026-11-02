using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Tests.Infrastructure;

public sealed class CustomerServiceTests
{
    [Fact]
    public async Task DeleteAsync_ShouldKeepSharedMeterAndParticipationUntilLastCustomerIsDeleted()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var customers = new CustomerService(unitOfWork);
        var meters = new MeteringPointService(unitOfWork);
        var communities = new EnergyCommunityService(unitOfWork);
        var first = await customers.CreateAsync("K-1", "First", null);
        var second = await customers.CreateAsync("K-2", "Second", null);
        var meter = await meters.CreateAsync([first.Id, second.Id], "AT-SHARED", "consumption", null);
        var community = await communities.CreateAsync("Shared community");
        await communities.SetParticipationAsync(community.Id, meter.Id, 75);
        dbContext.ChangeTracker.Clear();

        Assert.True(await customers.DeleteAsync(first.Id));
        dbContext.ChangeTracker.Clear();

        var loaded = await meters.GetByIdAsync(meter.Id);
        Assert.Equal(second.Id, Assert.Single(loaded!.Customers).Id);
        var participation = Assert.Single((await communities.GetByIdAsync(community.Id))!.Participations);
        Assert.Equal(75, participation.ParticipationPercentage);
        Assert.Equal(second.Id, Assert.Single(participation.MeteringPoint.Customers).Id);

        Assert.True(await customers.DeleteAsync(second.Id));
        dbContext.ChangeTracker.Clear();
        Assert.Null(await meters.GetByIdAsync(meter.Id));
        Assert.True((await dbContext.MeteringPoints.IgnoreQueryFilters().SingleAsync()).IsDeleted);
        Assert.Empty(await dbContext.Set<Dictionary<string, object>>("CustomerMeteringPoints").ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistCustomer()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var service = new CustomerService(unitOfWork);

        var customer = await service.CreateAsync("K-100", "Musterkunde", "kunde@example.com");

        Assert.Equal("K-100", customer.CustomerNumber);
        Assert.Equal("Musterkunde", customer.Name);
        Assert.NotEqual(Guid.Empty, customer.Id);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteCustomerAndMeteringPoints()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var customerService = new CustomerService(unitOfWork);
        var meteringPointService = new MeteringPointService(unitOfWork);

        var customer = await customerService.CreateAsync("K-200", "Delete Customer", null);
        var meteringPoint = await meteringPointService.CreateAsync([customer.Id], "AT001000000000000000000000000001", "generation", "Main Meter");

        var deleted = await customerService.DeleteAsync(customer.Id);

        Assert.True(deleted);
        Assert.Empty(await dbContext.Customers.ToListAsync());
        Assert.Empty(await dbContext.MeteringPoints.ToListAsync());

        var deletedCustomer = await dbContext.Customers.IgnoreQueryFilters().SingleAsync(x => x.Id == customer.Id);
        var deletedMeteringPoint = await dbContext.MeteringPoints.IgnoreQueryFilters().SingleAsync(x => x.Id == meteringPoint.Id);

        Assert.True(deletedCustomer.IsDeleted);
        Assert.True(deletedMeteringPoint.IsDeleted);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"egwn-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }
}
