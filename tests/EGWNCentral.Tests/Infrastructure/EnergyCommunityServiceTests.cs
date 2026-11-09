using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Tests.Infrastructure;

public sealed class EnergyCommunityServiceTests
{
    [Fact]
    public async Task GetBySourceAsync_FiltersAndLoadsParticipationsInNameOrder()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var service = new EnergyCommunityService(unitOfWork);
        var customer = await new CustomerService(unitOfWork).CreateAsync("K-1", "Customer", null);
        var meter = await new MeteringPointService(unitOfWork).CreateAsync([customer.Id], "AT-SHARED", "consumption", null);
        var second = await service.CreateAsync("Second", "EDA", "id-2");
        var first = await service.CreateAsync("First", "EDA", "id-1");
        await service.CreateAsync("Different", "EEGFaktura");
        await service.CreateAsync("Unknown");
        var deleted = await service.CreateAsync("Deleted", "EDA");
        await service.DeleteAsync(deleted.Id);
        await service.SetParticipationAsync(first.Id, meter.Id, 75);
        dbContext.ChangeTracker.Clear();

        var results = (await service.GetBySourceAsync("  EDA  ")).ToArray();

        Assert.Equal(new[] { first.Id, second.Id }, results.Select(x => x.Id));
        Assert.Equal("id-1", results[0].SourceId);
        Assert.Equal(customer.Id, Assert.Single(Assert.Single(results[0].Participations).MeteringPoint.Customers).Id);
        Assert.Empty(await service.GetBySourceAsync("eda"));
        Assert.Empty(await service.GetBySourceAsync("Missing"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetBySourceAsync_RejectsEmptySource(string source)
    {
        await using var dbContext = CreateDbContext();
        var service = new EnergyCommunityService(new UnitOfWork(dbContext));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetBySourceAsync(source));
    }

    [Fact]
    public async Task ParticipationQueries_ShouldLoadAllCustomersOfSharedMeter()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var customers = new CustomerService(unitOfWork);
        var first = await customers.CreateAsync("K-1", "First", null);
        var second = await customers.CreateAsync("K-2", "Second", null);
        var meter = await new MeteringPointService(unitOfWork).CreateAsync([first.Id, second.Id], "AT-SHARED", "consumption", null);
        var service = new EnergyCommunityService(unitOfWork);
        var community = await service.CreateAsync("Shared community");

        var participation = await service.SetParticipationAsync(community.Id, meter.Id, 75);
        Assert.Equal(2, participation.MeteringPoint.Customers.Count);
        dbContext.ChangeTracker.Clear();

        Assert.Equal(2, Assert.Single((await service.GetByIdAsync(community.Id))!.Participations).MeteringPoint.Customers.Count);
        Assert.Equal(2, Assert.Single(Assert.Single(await service.GetAllAsync()).Participations).MeteringPoint.Customers.Count);
    }

    [Fact]
    public async Task CreateAndUpdateAsync_ShouldNormalizeAndPersistSourceAndSourceId()
    {
        await using var dbContext = CreateDbContext();
        var service = new EnergyCommunityService(new UnitOfWork(dbContext));

        var community = await service.CreateAsync("Community One", "  EDA  ", "  external-123  ");

        Assert.Equal("EDA", community.Source);
        Assert.Equal("external-123", community.SourceId);

        dbContext.ChangeTracker.Clear();
        var persisted = await service.GetByIdAsync(community.Id);
        Assert.Equal("EDA", persisted?.Source);
        Assert.Equal("external-123", persisted?.SourceId);

        var updated = await service.UpdateAsync(community.Id, "Community One", "  EEGFaktura  ", "  invoice-456  ");

        Assert.Equal("EEGFaktura", updated?.Source);
        Assert.Equal("invoice-456", updated?.SourceId);

        dbContext.ChangeTracker.Clear();
        persisted = await service.GetByIdAsync(community.Id);
        Assert.Equal("EEGFaktura", persisted?.Source);
        Assert.Equal("invoice-456", persisted?.SourceId);

        var cleared = await service.UpdateAsync(community.Id, "Community One", null, "   ");

        Assert.Null(cleared?.Source);
        Assert.Null(cleared?.SourceId);
    }

    [Fact]
    public async Task SetParticipationAsync_AllowsMeteringPointInMultipleCommunitiesWithIndependentPercentages()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var customer = await new CustomerService(unitOfWork).CreateAsync("K-500", "Shared Customer", null);
        var meteringPoint = await new MeteringPointService(unitOfWork)
            .CreateAsync([customer.Id], "AT001000000000000000000000000005", "consumption", null);
        var communityService = new EnergyCommunityService(unitOfWork);
        var firstCommunity = await communityService.CreateAsync("Community One");
        var secondCommunity = await communityService.CreateAsync("Community Two");

        var firstParticipation = await communityService.SetParticipationAsync(firstCommunity.Id, meteringPoint.Id, 65);
        var secondParticipation = await communityService.SetParticipationAsync(secondCommunity.Id, meteringPoint.Id, 80);
        var updatedParticipation = await communityService.SetParticipationAsync(firstCommunity.Id, meteringPoint.Id, 35);

        Assert.Equal(65, firstParticipation.ParticipationPercentage);
        Assert.Equal(meteringPoint.Id, firstParticipation.MeteringPoint.Id);
        Assert.Equal(customer.CustomerNumber, Assert.Single(firstParticipation.MeteringPoint.Customers).CustomerNumber);
        Assert.Equal(80, secondParticipation.ParticipationPercentage);
        Assert.Equal(35, updatedParticipation.ParticipationPercentage);
        Assert.Equal(2, await dbContext.CommunityParticipations.CountAsync());

        var community = await communityService.GetByIdAsync(firstCommunity.Id);
        var participation = Assert.Single(community!.Participations);
        Assert.Equal(customer.Name, Assert.Single(participation.MeteringPoint.Customers).Name);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public async Task SetParticipationAsync_WithPercentageOutsideRange_ShouldThrow(decimal percentage)
    {
        await using var dbContext = CreateDbContext();
        var service = new EnergyCommunityService(new UnitOfWork(dbContext));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SetParticipationAsync(Guid.NewGuid(), Guid.NewGuid(), percentage));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"egwn-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }
}
