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
    public void ShortRegistrationMigration_ShouldConvertBeforeNarrowingAndEnforceFormat()
    {
        var migration = new ShortenCustomerRegistrationCode();
        var sql = Assert.IsType<SqlOperation>(migration.UpOperations[0]).Sql;
        Assert.Contains("CREATE FUNCTION generate_customer_registration_code()", sql);
        Assert.Contains("random_byte < 238", sql);
        Assert.Contains("UPDATE \"Customers\"", sql);
        Assert.Contains("UPDATE \"CustomerCreatedOutbox\"", sql);
        var column = Assert.Single(migration.UpOperations.OfType<AlterColumnOperation>());
        Assert.Equal(6, column.MaxLength);
        Assert.Equal("generate_customer_registration_code()", column.DefaultValueSql);
        Assert.Equal("\"RegistrationCode\" ~ '^[A-NP-Z1-9]{6}$'",
            Assert.Single(migration.UpOperations.OfType<AddCheckConstraintOperation>()).Sql);
    }

    [Fact]
    public void RegistrationCodeMigration_ShouldBackfillWithRandomDatabaseDefaultWithoutOverwritingSeeds()
    {
        var migration = new AddCustomerRegistrationCode();
        var column = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>());
        Assert.Equal("RegistrationCode", column.Name);
        Assert.False(column.IsNullable);
        Assert.Equal(32, column.MaxLength);
        Assert.Equal("upper(replace(gen_random_uuid()::text, '-', ''))", column.DefaultValueSql);
        var index = Assert.Single(migration.UpOperations.OfType<CreateIndexOperation>());
        Assert.Equal(new[] { "RegistrationCode" }, index.Columns);
        Assert.True(index.IsUnique);
        Assert.Null(index.Filter);
        Assert.DoesNotContain(migration.UpOperations, operation => operation is UpdateDataOperation);
        Assert.Equal(2, migration.UpOperations.Count);
    }

    [Fact]
    public void SplitEnergyCommunitySource_ShouldPreserveExistingSourceIds()
    {
        var migration = new SplitEnergyCommunitySource();
        var rename = Assert.Single(migration.UpOperations.OfType<RenameColumnOperation>());
        Assert.Equal("Quelle", rename.Name);
        Assert.Equal("SourceId", rename.NewName);
        var added = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>());
        Assert.Equal("Source", added.Name);
        Assert.True(added.IsNullable);
        Assert.DoesNotContain(migration.UpOperations, operation => operation is UpdateDataOperation);

        var rollbackRename = Assert.Single(migration.DownOperations.OfType<RenameColumnOperation>());
        Assert.Equal("SourceId", rollbackRename.Name);
        Assert.Equal("Quelle", rollbackRename.NewName);
        Assert.Equal("Source", Assert.Single(migration.DownOperations.OfType<DropColumnOperation>()).Name);
    }

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
