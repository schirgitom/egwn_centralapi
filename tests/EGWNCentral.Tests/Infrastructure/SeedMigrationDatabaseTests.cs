using EGWNCentral.Infrastructure.Messaging;
using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Services;
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
    private const string BeforeSourceSplit = "20261023154057_AddEnergyCommunitySource";

    [PostgreSqlFact]
    public async Task ShortRegistrationCodes_ShouldConvertExistingAndPendingMessagesAndSupportRollback()
    {
        await WithDatabaseAsync(async context =>
        {
            var migrator = context.GetService<IMigrator>();
            const string beforeShortCodes = "20261109175702_AddCustomerRegistrationCode";
            await migrator.MigrateAsync(beforeShortCodes);
            await context.Database.ExecuteSqlRawAsync("""
                UPDATE "Customers" SET "IsDeleted" = true WHERE "CustomerNumber" = '1';
                INSERT INTO "CustomerCreatedOutbox" ("Id", "OccurredAtUtc", "Payload")
                SELECT "Id", now(), jsonb_build_object(
                    'eventId', "Id", 'eventType', 'customer.created', 'schemaVersion', 1,
                    'customer', jsonb_build_object('id', "Id", 'registrationCode', "RegistrationCode"))::text
                FROM "Customers" WHERE "CustomerNumber" = '1';
                """);

            await context.Database.MigrateAsync();
            var customers = await context.Customers.IgnoreQueryFilters().AsNoTracking().ToListAsync();
            Assert.All(customers, customer => Assert.Matches("^[A-NP-Z1-9]{6}$", customer.RegistrationCode));
            Assert.Equal(customers.Count, customers.Select(customer => customer.RegistrationCode).Distinct().Count());
            var deleted = Assert.Single(customers, customer => customer.CustomerNumber == "1");
            using var payload = System.Text.Json.JsonDocument.Parse((await context.OutboxMessages.SingleAsync()).Payload);
            Assert.Equal(deleted.RegistrationCode, payload.RootElement.GetProperty("customer").GetProperty("registrationCode").GetString());

            await context.Database.ExecuteSqlRawAsync("""
                INSERT INTO "Customers" ("Id", "CustomerNumber", "Name", "CreatedAtUtc", "IsDeleted")
                VALUES (gen_random_uuid(), 'CODE-DEFAULT', 'Default code', now(), false);
                """);
            var generated = await context.Customers.AsNoTracking().SingleAsync(customer => customer.CustomerNumber == "CODE-DEFAULT");
            Assert.Matches("^[A-NP-Z1-9]{6}$", generated.RegistrationCode);
            await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
                INSERT INTO "Customers" ("Id", "CustomerNumber", "Name", "CreatedAtUtc", "IsDeleted", "RegistrationCode")
                VALUES (gen_random_uuid(), 'INVALID-CODE', 'Invalid code', now(), false, 'ABC120');
                """));

            await migrator.MigrateAsync(beforeShortCodes);
            await context.Database.MigrateAsync();
            var reloaded = await context.Customers.IgnoreQueryFilters().AsNoTracking().SingleAsync(customer => customer.Id == deleted.Id);
            Assert.Equal(deleted.RegistrationCode, reloaded.RegistrationCode);
        });
    }

    [PostgreSqlFact]
    public async Task RegistrationCodes_ShouldBackfillAllCustomersAndEnforceUniqueness()
    {
        await WithDatabaseAsync(async context =>
        {
            await context.GetService<IMigrator>().MigrateAsync("20261109171103_AddCustomerCreatedOutbox");
            await context.Database.ExecuteSqlRawAsync("""
                UPDATE "Customers" SET "IsDeleted" = true WHERE "CustomerNumber" = '1';
                """);
            await context.Database.MigrateAsync();
            var customers = await context.Customers.IgnoreQueryFilters().AsNoTracking().ToListAsync();
            Assert.Equal(459, customers.Count);
            Assert.All(customers, customer => Assert.Matches("^[A-NP-Z1-9]{6}$", customer.RegistrationCode));
            Assert.Equal(customers.Count, customers.Select(customer => customer.RegistrationCode).Distinct().Count());
            Assert.Empty(await context.OutboxMessages.ToListAsync());
            var originalCodes = customers.ToDictionary(customer => customer.Id, customer => customer.RegistrationCode);
            await context.Database.MigrateAsync();
            Assert.All(await context.Customers.IgnoreQueryFilters().AsNoTracking().ToListAsync(),
                customer => Assert.Equal(originalCodes[customer.Id], customer.RegistrationCode));

            var deletedCustomer = Assert.Single(customers, customer => customer.IsDeleted);
            context.Customers.Add(new EGWNCentral.Core.Domain.Entities.Customer
            {
                CustomerNumber = "DUPLICATE-CODE", Name = "Duplicate", RegistrationCode = deletedCustomer.RegistrationCode
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();
            Assert.Empty(await context.OutboxMessages.ToListAsync());
            Assert.Equal(459, await context.Customers.IgnoreQueryFilters().CountAsync());
        });
    }

    [PostgreSqlFact]
    public async Task CustomerSnapshots_ShouldPersistRelationsAndRollbackWithFailedBusinessWrite()
    {
        await WithDatabaseAsync(async context =>
        {
            await context.Database.MigrateAsync();
            var unit = new UnitOfWork(context);
            var customers = new CustomerService(unit);
            var customer = await customers.CreateAsync("OUTBOX-TEST", "Outbox", null);
            var meters = new MeteringPointService(unit);
            var meter = await meters.CreateAsync([customer.Id], "AT-OUTBOX-TEST", "consumption", null);
            var communities = new EnergyCommunityService(unit);
            var community = await communities.CreateAsync("Outbox community");
            context.OutboxMessages.RemoveRange(await context.OutboxMessages.ToListAsync());
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            await communities.SetParticipationAsync(community.Id, meter.Id, 65);
            var payload = (await context.OutboxMessages.SingleAsync()).Payload;
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<CustomerSnapshot>(payload,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
            Assert.Equal(65, Assert.Single(Assert.Single(snapshot.Customer.EnergyCommunities).MeteringPoints).ParticipationPercentage);

            context.OutboxMessages.RemoveRange(await context.OutboxMessages.ToListAsync());
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            var tracked = await context.Customers.SingleAsync(x => x.Id == customer.Id);
            tracked.Name = new string('x', 201);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();
            Assert.Empty(await context.OutboxMessages.ToListAsync());
            Assert.Equal("Outbox", (await context.Customers.SingleAsync(x => x.Id == customer.Id)).Name);
        });
    }

    [PostgreSqlFact]
    public async Task SourceSplit_ShouldPreserveLegacyIdsOnMigrationAndRollback()
    {
        await WithDatabaseAsync(async context =>
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(BeforeSourceSplit);
            await context.Database.ExecuteSqlRawAsync("""
                UPDATE "EnergyCommunities" SET "Quelle" = 'legacy-id'
                WHERE "Id" = '23ac2aa2-5adf-506e-dae0-3fc21c74093b';
                INSERT INTO "EnergyCommunities" ("Id", "Name", "Quelle", "CreatedAtUtc", "IsDeleted")
                VALUES ('00000000-0000-0000-0000-000000000006', 'Existing community', 'custom-id', now(), false);
                """);

            await context.Database.MigrateAsync();
            Assert.Equal(2, await ScalarAsync(context, """
                SELECT count(*) FROM "EnergyCommunities"
                WHERE ("SourceId" = 'legacy-id' OR "SourceId" = 'custom-id') AND "Source" IS NULL
                """));
            await context.Database.ExecuteSqlRawAsync("""
                UPDATE "EnergyCommunities" SET "Source" = 'EDA' WHERE "SourceId" = 'legacy-id'
                """);

            await migrator.MigrateAsync(BeforeSourceSplit);
            Assert.Equal(2, await ScalarAsync(context, """
                SELECT count(*) FROM "EnergyCommunities" WHERE "Quelle" IN ('legacy-id', 'custom-id')
                """));
            await context.Database.MigrateAsync();
            Assert.Equal(2, await ScalarAsync(context, """
                SELECT count(*) FROM "EnergyCommunities"
                WHERE "SourceId" IN ('legacy-id', 'custom-id') AND "Source" IS NULL
                """));
        });
    }

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
