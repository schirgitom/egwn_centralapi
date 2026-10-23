using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Tests.Infrastructure;

public sealed class EnergyCommunityServiceTests
{
    [Fact]
    public async Task CreateAndUpdateAsync_ShouldNormalizeAndPersistSource()
    {
        await using var dbContext = CreateDbContext();
        var service = new EnergyCommunityService(new UnitOfWork(dbContext));

        var community = await service.CreateAsync("Community One", "  energy-provider  ");

        Assert.Equal("energy-provider", community.Source);

        var updated = await service.UpdateAsync(community.Id, "Community One", "  market-api  ");

        Assert.Equal("market-api", updated?.Source);

        var cleared = await service.UpdateAsync(community.Id, "Community One", null);

        Assert.Null(cleared?.Source);
    }

    [Fact]
    public async Task SetParticipationAsync_AllowsMeteringPointInMultipleCommunitiesWithIndependentPercentages()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new UnitOfWork(dbContext);
        var customer = await new CustomerService(unitOfWork).CreateAsync("K-500", "Shared Customer", null);
        var meteringPoint = await new MeteringPointService(unitOfWork)
            .CreateAsync(customer.Id, "AT001000000000000000000000000005", "consumption", null);
        var communityService = new EnergyCommunityService(unitOfWork);
        var firstCommunity = await communityService.CreateAsync("Community One");
        var secondCommunity = await communityService.CreateAsync("Community Two");

        var firstParticipation = await communityService.SetParticipationAsync(firstCommunity.Id, meteringPoint.Id, 65);
        var secondParticipation = await communityService.SetParticipationAsync(secondCommunity.Id, meteringPoint.Id, 80);
        var updatedParticipation = await communityService.SetParticipationAsync(firstCommunity.Id, meteringPoint.Id, 35);

        Assert.Equal(65, firstParticipation.ParticipationPercentage);
        Assert.Equal(meteringPoint.Id, firstParticipation.MeteringPoint.Id);
        Assert.Equal(customer.CustomerNumber, firstParticipation.MeteringPoint.Customer.CustomerNumber);
        Assert.Equal(80, secondParticipation.ParticipationPercentage);
        Assert.Equal(35, updatedParticipation.ParticipationPercentage);
        Assert.Equal(2, await dbContext.CommunityParticipations.CountAsync());

        var community = await communityService.GetByIdAsync(firstCommunity.Id);
        var participation = Assert.Single(community!.Participations);
        Assert.Equal(customer.Name, participation.MeteringPoint.Customer.Name);
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
