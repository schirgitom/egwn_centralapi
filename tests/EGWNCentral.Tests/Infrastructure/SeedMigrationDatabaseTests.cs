using EGWNCentral.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace EGWNCentral.Tests.Infrastructure;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EGWN_TEST_POSTGRES")))
        {
            Skip = "Set EGWN_TEST_POSTGRES to a PostgreSQL connection with CREATE DATABASE permission.";
        }
    }
}

public sealed class SeedMigrationDatabaseTests
{
    private const string BeforeSharing = "20261009165954_AddEnergyCommunitiesAndParticipations";
    private const string Sharing = "20261021173403_ShareMeteringPointsBetweenCustomers";

    [PostgreSqlFact]
    public async Task FreshDatabase_ShouldSupportRollbackAndReapply()
    {
        await WithDatabaseAsync(async context =>
        {
            await context.Database.MigrateAsync();
            Assert.Equal(681, await ScalarAsync(context, """SELECT count(*) FROM "MeteringPoints" """));
            Assert.Equal(681, await ScalarAsync(context, """SELECT count(*) FROM "CustomerMeteringPoints" """));
            Assert.Equal(684, await ScalarAsync(context, """SELECT count(*) FROM "CommunityParticipations" """));
            await context.GetService<IMigrator>().MigrateAsync(BeforeSharing);
            Assert.Equal(0, await ScalarAsync(context, """SELECT count(*) FROM "Customers" """));
            Assert.Equal(0, await ScalarAsync(context, """SELECT count(*) FROM "MeteringPoints" """));
            await context.Database.MigrateAsync();
            Assert.Equal(681, await ScalarAsync(context, """SELECT count(*) FROM "MeteringPoints" """));
        });
    }

    [PostgreSqlFact]
    public async Task ExistingSeedKeysWithDifferentIds_ShouldKeepDataAndRollbackOnlyNewRecords()
    {
        await WithDatabaseAsync(async context =>
        {
            await ArrangeExistingDataAsync(context);
            await context.Database.MigrateAsync();
            Assert.Equal(681, await ScalarAsync(context, """SELECT count(*) FROM "MeteringPoints" """));
            Assert.Equal(460, await ScalarAsync(context, """SELECT count(*) FROM "Customers" """));
            Assert.Equal(684, await ScalarAsync(context, """SELECT count(*) FROM "CommunityParticipations" """));
            Assert.Equal(2, await ScalarAsync(context, """
                SELECT count(*) FROM "CustomerMeteringPoints"
                WHERE "MeteringPointId" = '00000000-0000-0000-0000-000000000003'
                """));
            Assert.Equal(1, await ScalarAsync(context, """
                SELECT count(*) FROM "CommunityParticipations"
                WHERE "Id" = '00000000-0000-0000-0000-000000000005'
                  AND "ParticipationPercentage" = 37.5
                """));
            Assert.Equal(1, await ScalarAsync(context, """
                SELECT count(*) FROM "Customers"
                WHERE "Id" = '00000000-0000-0000-0000-000000000002' AND "Name" = 'Existing seeded customer'
                """));
            Assert.Equal(1, await ScalarAsync(context, """
                SELECT count(*) FROM "MeteringPoints"
                WHERE "Id" = '00000000-0000-0000-0000-000000000003' AND "Description" = 'Keep description'
                """));

            await context.GetService<IMigrator>().MigrateAsync(Sharing);
            Assert.Equal(2, await ScalarAsync(context, """SELECT count(*) FROM "Customers" """));
            Assert.Equal(1, await ScalarAsync(context, """SELECT count(*) FROM "MeteringPoints" """));
            Assert.Equal(1, await ScalarAsync(context, """SELECT count(*) FROM "EnergyCommunities" """));
            Assert.Equal(1, await ScalarAsync(context, """SELECT count(*) FROM "CommunityParticipations" """));
            Assert.Equal(1, await ScalarAsync(context, """
                SELECT count(*) FROM "CustomerMeteringPoints"
                WHERE "CustomerId" = '00000000-0000-0000-0000-000000000001'
                  AND "MeteringPointId" = '00000000-0000-0000-0000-000000000003'
                """));
            await context.Database.MigrateAsync();
            Assert.Equal(681, await ScalarAsync(context, """SELECT count(*) FROM "MeteringPoints" """));
        });
    }

    [PostgreSqlFact]
    public async Task ConflictingEnergyDirection_ShouldFailAtomically()
    {
        await WithDatabaseAsync(async context =>
        {
            await ArrangeExistingDataAsync(context);
            await context.Database.ExecuteSqlRawAsync("""UPDATE "MeteringPoints" SET "EnergyDirection" = 'consumption' """);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.MigrateAsync());
            Assert.Contains("different energy direction", exception.MessageText);
            Assert.Equal(2, await ScalarAsync(context, """SELECT count(*) FROM "Customers" """));
            Assert.Equal(1, await ScalarAsync(context, """SELECT count(*) FROM "MeteringPoints" """));
            Assert.Equal(0, await ScalarAsync(context, """
                SELECT count(*) FROM information_schema.tables
                WHERE table_name = '__SeedCurrentCustomersAndCommunities'
                """));
        });
    }

    [PostgreSqlFact]
    public async Task AmbiguousCommunityName_ShouldFailWithoutChangingExistingData()
    {
        await WithDatabaseAsync(async context =>
        {
            await ArrangeExistingDataAsync(context);
            await context.Database.ExecuteSqlRawAsync("""
                INSERT INTO "EnergyCommunities" ("Id", "Name", "CreatedAtUtc", "IsDeleted")
                VALUES ('00000000-0000-0000-0000-000000000006', 'EEG TWS', now(), false)
                """);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.MigrateAsync());
            Assert.Contains("multiple active EnergyCommunities", exception.MessageText);
            Assert.Equal(2, await ScalarAsync(context, """SELECT count(*) FROM "Customers" """));
            Assert.Equal(1, await ScalarAsync(context, """SELECT count(*) FROM "MeteringPoints" """));
            Assert.Equal(2, await ScalarAsync(context, """SELECT count(*) FROM "EnergyCommunities" """));
        });
    }

    [PostgreSqlFact]
    public async Task MissingOwnershipLedger_ShouldBlockRollback()
    {
        await WithDatabaseAsync(async context =>
        {
            await context.Database.MigrateAsync();
            await context.Database.ExecuteSqlRawAsync("""DROP TABLE "__SeedCurrentCustomersAndCommunities" """);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(Sharing));
            Assert.Contains("ownership ledger is missing", exception.MessageText);
            Assert.Equal(681, await ScalarAsync(context, """SELECT count(*) FROM "MeteringPoints" """));
            Assert.Equal(684, await ScalarAsync(context, """SELECT count(*) FROM "CommunityParticipations" """));
        });
    }

    [PostgreSqlFact]
    public async Task Rollback_WithNewAssignmentToSeededMeter_ShouldFailWithoutLosingData()
    {
        await WithDatabaseAsync(async context =>
        {
            await ArrangeExistingDataAsync(context);
            await context.Database.MigrateAsync();
            await context.Database.ExecuteSqlRawAsync("""
                INSERT INTO "CustomerMeteringPoints" ("CustomerId", "MeteringPointId")
                SELECT '00000000-0000-0000-0000-000000000001', "Id"
                FROM "MeteringPoints" WHERE "MeteringPointCode" = 'AT0082000816000000000000000935801'
                """);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(Sharing));
            Assert.Contains("additional customer assignments", exception.MessageText);
            Assert.Equal(681, await ScalarAsync(context, """SELECT count(*) FROM "MeteringPoints" """));
            Assert.Equal(684, await ScalarAsync(context, """SELECT count(*) FROM "CommunityParticipations" """));
        });
    }

    private static async Task ArrangeExistingDataAsync(ApplicationDbContext context)
    {
        await context.GetService<IMigrator>().MigrateAsync(BeforeSharing);
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Customers" ("Id", "CustomerNumber", "Name", "CreatedAtUtc", "IsDeleted")
            VALUES ('00000000-0000-0000-0000-000000000001', 'EXISTING-ONLY', 'Existing owner', now(), false),
                   ('00000000-0000-0000-0000-000000000002', '379', 'Existing seeded customer', now(), false);
            INSERT INTO "MeteringPoints"
                ("Id", "CustomerId", "MeteringPointCode", "Description", "EnergyDirection", "CreatedAtUtc", "IsDeleted")
            VALUES ('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001',
                    'AT0080000816000000202301310228682', 'Keep description', 'generation', now(), false);
            INSERT INTO "EnergyCommunities" ("Id", "Name", "CreatedAtUtc", "IsDeleted")
            VALUES ('00000000-0000-0000-0000-000000000004', 'EEG TWS', now(), false);
            INSERT INTO "CommunityParticipations"
                ("Id", "EnergyCommunityId", "MeteringPointId", "ParticipationPercentage", "CreatedAtUtc", "IsDeleted")
            VALUES ('00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000004',
                    '00000000-0000-0000-0000-000000000003', 37.5, now(), false);
            """);
    }

    private static async Task<long> ScalarAsync(ApplicationDbContext context, string sql)
    {
        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task WithDatabaseAsync(Func<ApplicationDbContext, Task> test)
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("EGWN_TEST_POSTGRES")
            ?? throw new InvalidOperationException("EGWN_TEST_POSTGRES is required.");
        var name = $"egwn_seed_test_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin))
        {
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var connectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = name, Pooling = false };
            await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString.ConnectionString).Options);
            await test(context);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
