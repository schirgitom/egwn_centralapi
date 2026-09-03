using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Tests.Infrastructure;

public sealed class MeteringPointServiceTests
{
    [Fact]
    public async Task CreateAsync_WithUnknownCustomer_ShouldThrow()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var service = new MeteringPointService(unitOfWork);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateAsync(Guid.NewGuid(), "AT001000000000000000000000000002", "generation", "Missing Customer"));
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
            service.CreateAsync(customer.Id, "AT001000000000000000000000000003", "invalid", "Old"));

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
        var meteringPoint = await meteringPointService.CreateAsync(customer.Id, "AT001000000000000000000000000003", "generation", "Old");

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
        var meteringPoint = await meteringPointService.CreateAsync(customer.Id, "AT001000000000000000000000000004", "generation", null);

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
