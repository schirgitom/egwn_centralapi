using EGWNCentral.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Tests.Infrastructure;

public sealed class SeedDataTests
{
    [Fact]
    public async Task ModelManagedSeed_ShouldContainCustomersMetersCommunitiesAndFullParticipations()
    {
        var databaseName = $"egwn-seed-tests-{Guid.NewGuid()}";
        await using var dbContext = CreateDbContext(databaseName);
        await dbContext.Database.EnsureCreatedAsync();

        Assert.Equal(459, await dbContext.Customers.CountAsync());
        Assert.Equal(681, await dbContext.MeteringPoints.CountAsync());
        Assert.Equal(11, await dbContext.EnergyCommunities.CountAsync());
        var communityNames = await dbContext.EnergyCommunities
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToArrayAsync();
        Assert.Equal(
            new[] { "EEG TE", "EEG TW", "EEG TWS", "EEG U", "EEG WN", "EEG WS", "EEG WTE", "EEG WTE ST", "EEG XX", "GEA B65", "GEA U11" },
            communityNames);
        Assert.Equal(684, await dbContext.CommunityParticipations.CountAsync());
        Assert.All(
            await dbContext.CommunityParticipations.ToListAsync(),
            participation => Assert.Equal(100m, participation.ParticipationPercentage));

        var customer = await dbContext.Customers.SingleAsync(x => x.Name == "Nistelberger Veronika");
        Assert.Equal("6", customer.CustomerNumber);

        var sharedMeter = await dbContext.MeteringPoints
            .Include(x => x.Customer)
            .SingleAsync(x => x.MeteringPointCode == "AT0080000816000000201803050055335");
        Assert.Equal("9", sharedMeter.Customer.CustomerNumber);
        Assert.Equal("generation", sharedMeter.EnergyDirection);
        Assert.Equal(2, await dbContext.CommunityParticipations.CountAsync(x => x.MeteringPointId == sharedMeter.Id));

        await using var secondContext = CreateDbContext(databaseName);
        Assert.Equal(459, await secondContext.Customers.CountAsync());
        Assert.Equal(681, await secondContext.MeteringPoints.CountAsync());
        Assert.Equal(11, await secondContext.EnergyCommunities.CountAsync());
        Assert.Equal(684, await secondContext.CommunityParticipations.CountAsync());
    }

    private static ApplicationDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }
}
