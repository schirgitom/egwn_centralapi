using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace EGWNCentral.Tests.Infrastructure;

public sealed class MigrationTests
{
    [Fact]
    public void SeedMigration_ShouldMergeExistingRecordsAndTrackInsertedData()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=migration-script-only")
            .Options;
        using var context = new ApplicationDbContext(options);
        var migration = new SeedCurrentCustomersAndCommunities();
        var up = string.Join("\n", migration.UpOperations.Cast<SqlOperation>().Select(x => x.Sql));
        var down = string.Join("\n", migration.DownOperations.Cast<SqlOperation>().Select(x => x.Sql));
        Assert.Contains("CREATE TABLE \"__SeedCurrentCustomersAndCommunities\"", up);
        Assert.Contains("LOCK TABLE", up);
        Assert.Contains("CREATE TEMP TABLE \"Seed_Map_MeteringPoints\"", up);
        Assert.Contains("existing.\"MeteringPointCode\" = s.\"MeteringPointCode\"", up);
        Assert.Contains("existing.\"CustomerNumber\" = s.\"CustomerNumber\"", up);
        Assert.Contains("SELECT c.\"ActualId\", m.\"ActualId\"", up);
        Assert.Contains("existing.\"EnergyCommunityId\" = c.\"ActualId\"", up);
        Assert.Contains("Seed conflict: existing metering point has a different energy direction.", up);
        Assert.Contains("ownership ledger is missing", down);
        Assert.Contains("seeded records have additional customer assignments", down);
        Assert.Contains("p.\"Id\" = s.\"Id\"", down);

        Assert.False(context.Database.HasPendingModelChanges());
        var script = context.GetService<IMigrator>().GenerateScript();
        Assert.Contains("INSERT INTO \"CustomerMeteringPoints\"", script);
        Assert.DoesNotContain("\"CreatedAtUtc\", \"CustomerId\"", script);
    }

    [Theory]
    [InlineData("Customers", 459)]
    [InlineData("MeteringPoints", 681)]
    [InlineData("EnergyCommunities", 11)]
    [InlineData("CustomerMeteringPoints", 681)]
    [InlineData("CommunityParticipations", 684)]
    public void StagingData_ShouldKeepAllOriginalSeedRows(string table, int expectedCount)
    {
        var migration = new SeedCurrentCustomersAndCommunities();
        var insert = Assert.Single(migration.UpOperations.Cast<SqlOperation>(),
            x => x.Sql.StartsWith($"INSERT INTO \"Seed_{table}\""));
        Assert.Equal(expectedCount, insert.Sql.Split("VALUES ", 2)[1].Split('\n').Length);
    }
}
