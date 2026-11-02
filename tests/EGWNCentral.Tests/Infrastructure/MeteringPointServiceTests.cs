using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Tests.Infrastructure;

public sealed class MeteringPointServiceTests
{
    [Fact]
    public async Task CreateAndReadAsync_ShouldPersistAllDistinctCustomers()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var customerService = new CustomerService(unitOfWork);
        var service = new MeteringPointService(unitOfWork);
        var first = await customerService.CreateAsync("K-1", "First", null);
        var second = await customerService.CreateAsync("K-2", "Second", null);

        var meter = await service.CreateAsync([first.Id, second.Id, first.Id], "AT-SHARED", "consumption", null);
        dbContext.ChangeTracker.Clear();

        var loaded = await service.GetByIdAsync(meter.Id);
        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(x => x), loaded!.Customers.Select(x => x.Id).OrderBy(x => x));
        Assert.Equal(2, Assert.Single(await service.GetAllAsync()).Customers.Count);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReplaceCustomersAndPreserveThemWhenOmitted()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var customerService = new CustomerService(unitOfWork);
        var service = new MeteringPointService(unitOfWork);
        var first = await customerService.CreateAsync("K-1", "First", null);
        var second = await customerService.CreateAsync("K-2", "Second", null);
        var third = await customerService.CreateAsync("K-3", "Third", null);
        var meter = await service.CreateAsync([first.Id, second.Id], "AT-SHARED", "consumption", null);
        dbContext.ChangeTracker.Clear();

        await service.UpdateAsync(meter.Id, null, null, [second.Id, third.Id, third.Id]);
        dbContext.ChangeTracker.Clear();
        await service.UpdateAsync(meter.Id, "New description", null);
        dbContext.ChangeTracker.Clear();

        var loaded = await service.GetByIdAsync(meter.Id);
        Assert.Equal(new[] { second.Id, third.Id }.OrderBy(x => x), loaded!.Customers.Select(x => x.Id).OrderBy(x => x));
        Assert.Equal("New description", loaded.Description);
    }

    [Fact]
    public async Task CreateAndUpdateAsync_ShouldRejectEmptyCustomerAssignments()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var service = new MeteringPointService(unitOfWork);
        var customer = await new CustomerService(unitOfWork).CreateAsync("K-1", "First", null);
        var meter = await service.CreateAsync([customer.Id], "AT-SHARED", "consumption", null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync([], "AT-EMPTY", "consumption", null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync([Guid.Empty], "AT-EMPTY", "consumption", null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(meter.Id, null, null, []));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(meter.Id, null, null, [Guid.Empty]));
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownOrDeletedCustomer_ShouldNotChangeMeter()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var customerService = new CustomerService(unitOfWork);
        var service = new MeteringPointService(unitOfWork);
        var customer = await customerService.CreateAsync("K-1", "First", null);
        var deleted = await customerService.CreateAsync("K-2", "Deleted", null);
        await customerService.DeleteAsync(deleted.Id);
        var meter = await service.CreateAsync([customer.Id], "AT-SHARED", "consumption", "Old");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateAsync([customer.Id, deleted.Id], "AT-INVALID", "consumption", null));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateAsync(meter.Id, "Changed", "generation", [customer.Id, Guid.NewGuid()]));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateAsync(meter.Id, "Changed", "generation", [deleted.Id]));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var loaded = await service.GetByIdAsync(meter.Id);
        Assert.Equal(customer.Id, Assert.Single(loaded!.Customers).Id);
        Assert.Equal("Old", loaded.Description);
        Assert.Equal("consumption", loaded.EnergyDirection);
        Assert.Single(await service.GetAllAsync());
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateMeterCode_ShouldStillThrow()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var service = new MeteringPointService(unitOfWork);
        var customer = await new CustomerService(unitOfWork).CreateAsync("K-1", "First", null);
        await service.CreateAsync([customer.Id], "AT-SHARED", "consumption", null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync([customer.Id], " AT-SHARED ", "generation", null));
    }

    [Fact]
    public async Task CreateAsync_WithUnknownCustomer_ShouldThrow()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var service = new MeteringPointService(unitOfWork);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateAsync([Guid.NewGuid()], "AT001000000000000000000000000002", "generation", "Missing Customer"));
    }

    [Fact]
    public async Task CreateAsync_WithInvalidEnergyDirection_ShouldThrow()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var customerService = new CustomerService(unitOfWork);
        var service = new MeteringPointService(unitOfWork);

        var customer = await customerService.CreateAsync("K-300", "Invalid Energy Direction", null);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync([customer.Id], "AT001000000000000000000000000003", "invalid", "Old"));

        Assert.Equal("energyDirection", ex.ParamName);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateDescriptionAndEnergyDirection()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var customerService = new CustomerService(unitOfWork);
        var meteringPointService = new MeteringPointService(unitOfWork);

        var customer = await customerService.CreateAsync("K-300", "Update Customer", null);
        var meteringPoint = await meteringPointService.CreateAsync([customer.Id], "AT001000000000000000000000000003", "generation", "Old");

        var updated = await meteringPointService.UpdateAsync(meteringPoint.Id, "New", "consumption");

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Description);
        Assert.Equal("consumption", updated.EnergyDirection);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteMeteringPoint()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var customerService = new CustomerService(unitOfWork);
        var meteringPointService = new MeteringPointService(unitOfWork);

        var customer = await customerService.CreateAsync("K-400", "Delete Meter", null);
        var meteringPoint = await meteringPointService.CreateAsync([customer.Id], "AT001000000000000000000000000004", "generation", null);

        var deleted = await meteringPointService.DeleteAsync(meteringPoint.Id);

        Assert.True(deleted);
        Assert.Empty(await dbContext.MeteringPoints.ToListAsync());

        var deletedMeteringPoint = await dbContext.MeteringPoints.IgnoreQueryFilters().SingleAsync(x => x.Id == meteringPoint.Id);
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
