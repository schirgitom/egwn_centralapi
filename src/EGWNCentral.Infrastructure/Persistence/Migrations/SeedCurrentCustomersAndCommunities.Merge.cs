using System.Globalization;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace EGWNCentral.Infrastructure.Persistence.Migrations;

public partial class SeedCurrentCustomersAndCommunities
{
    private const string Ledger = "__SeedCurrentCustomersAndCommunities";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            LOCK TABLE "Customers", "MeteringPoints", "EnergyCommunities",
                "CustomerMeteringPoints", "CommunityParticipations" IN SHARE ROW EXCLUSIVE MODE;
            CREATE TABLE "__SeedCurrentCustomersAndCommunities" (
                "Entity" text NOT NULL,
                "Id" uuid NOT NULL,
                "RelatedId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                PRIMARY KEY ("Entity", "Id", "RelatedId")
            );
            """);

        var seeds = new MigrationBuilder(migrationBuilder.ActiveProvider);
        BuildSeedData(seeds);
        foreach (var insert in seeds.Operations.Cast<InsertDataOperation>())
        {
            var table = insert.Table;
            migrationBuilder.Sql($"""
                CREATE TEMP TABLE "Seed_{table}" (LIKE "{table}") ON COMMIT DROP;
                """);
            // Staging tables are not part of EF's target model, so supply explicit SQL literals.
            var rows = Enumerable.Range(0, insert.Values.GetLength(0))
                .Select(row => "(" + string.Join(", ", Enumerable.Range(0, insert.Columns.Length)
                    .Select(column => Literal(insert.Values[row, column]))) + ")");
            migrationBuilder.Sql($"""
                INSERT INTO "Seed_{table}" ({Columns(insert.Columns)})
                VALUES {string.Join(",\n", rows)};
                """);
        }

        MergeEntity(migrationBuilder, "Customers", "CustomerNumber",
            ["Id", "CreatedAtUtc", "CustomerNumber", "DeletedAtUtc", "Email", "IsDeleted", "Name", "UpdatedAtUtc"]);
        MergeEntity(migrationBuilder, "MeteringPoints", "MeteringPointCode",
            ["Id", "CreatedAtUtc", "DeletedAtUtc", "Description", "EnergyDirection", "IsDeleted", "MeteringPointCode", "UpdatedAtUtc"]);
        MergeEntity(migrationBuilder, "EnergyCommunities", "Name",
            ["Id", "CreatedAtUtc", "DeletedAtUtc", "IsDeleted", "Name", "UpdatedAtUtc"]);

        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM "Seed_MeteringPoints" s
                    JOIN "Seed_Map_MeteringPoints" m ON m."SeedId" = s."Id"
                    JOIN "MeteringPoints" p ON p."Id" = m."ActualId"
                    WHERE p."EnergyDirection" <> s."EnergyDirection"
                ) THEN
                    RAISE EXCEPTION 'Seed conflict: existing metering point has a different energy direction.';
                END IF;
            END $$;

            WITH inserted AS (
                INSERT INTO "CustomerMeteringPoints" ("CustomerId", "MeteringPointId")
                SELECT c."ActualId", m."ActualId"
                FROM "Seed_CustomerMeteringPoints" s
                JOIN "Seed_Map_Customers" c ON c."SeedId" = s."CustomerId"
                JOIN "Seed_Map_MeteringPoints" m ON m."SeedId" = s."MeteringPointId"
                WHERE NOT EXISTS (
                    SELECT 1 FROM "CustomerMeteringPoints" existing
                    WHERE existing."CustomerId" = c."ActualId" AND existing."MeteringPointId" = m."ActualId"
                )
                RETURNING "CustomerId", "MeteringPointId"
            )
            INSERT INTO "__SeedCurrentCustomersAndCommunities" ("Entity", "Id", "RelatedId")
            SELECT 'CustomerMeteringPoints', "CustomerId", "MeteringPointId" FROM inserted;

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM "Seed_CommunityParticipations" s
                    JOIN "Seed_Map_EnergyCommunities" c ON c."SeedId" = s."EnergyCommunityId"
                    JOIN "Seed_Map_MeteringPoints" m ON m."SeedId" = s."MeteringPointId"
                    JOIN "CommunityParticipations" existing ON existing."Id" = s."Id"
                    WHERE existing."IsDeleted" OR existing."EnergyCommunityId" <> c."ActualId"
                       OR existing."MeteringPointId" <> m."ActualId"
                ) THEN
                    RAISE EXCEPTION 'Seed conflict: participation seed ID belongs to a deleted or different record.';
                END IF;
            END $$;

            WITH inserted AS (
                INSERT INTO "CommunityParticipations"
                    ("Id", "CreatedAtUtc", "DeletedAtUtc", "EnergyCommunityId", "IsDeleted",
                     "MeteringPointId", "ParticipationPercentage", "UpdatedAtUtc")
                SELECT s."Id", s."CreatedAtUtc", s."DeletedAtUtc", c."ActualId", s."IsDeleted",
                       m."ActualId", s."ParticipationPercentage", s."UpdatedAtUtc"
                FROM "Seed_CommunityParticipations" s
                JOIN "Seed_Map_EnergyCommunities" c ON c."SeedId" = s."EnergyCommunityId"
                JOIN "Seed_Map_MeteringPoints" m ON m."SeedId" = s."MeteringPointId"
                WHERE NOT EXISTS (
                    SELECT 1 FROM "CommunityParticipations" existing
                    WHERE existing."EnergyCommunityId" = c."ActualId"
                      AND existing."MeteringPointId" = m."ActualId" AND NOT existing."IsDeleted"
                )
                RETURNING "Id"
            )
            INSERT INTO "__SeedCurrentCustomersAndCommunities" ("Entity", "Id")
            SELECT 'CommunityParticipations', "Id" FROM inserted;
            """);
    }

    private static void MergeEntity(MigrationBuilder migrationBuilder, string table, string key, string[] columns)
    {
        migrationBuilder.Sql($$"""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT s."Id" FROM "Seed_{{table}}" s
                    JOIN "{{table}}" existing ON existing."{{key}}" = s."{{key}}" AND NOT existing."IsDeleted"
                    GROUP BY s."Id" HAVING count(*) > 1
                ) THEN
                    RAISE EXCEPTION 'Seed conflict: multiple active {{table}} match the seed key.';
                END IF;
                IF EXISTS (
                    SELECT 1 FROM "Seed_{{table}}" s
                    JOIN "{{table}}" existing ON existing."Id" = s."Id"
                    WHERE existing."IsDeleted" OR existing."{{key}}" <> s."{{key}}"
                ) THEN
                    RAISE EXCEPTION 'Seed conflict: {{table}} seed ID belongs to a deleted or different record.';
                END IF;
            END $$;

            WITH inserted AS (
                INSERT INTO "{{table}}" ({{Columns(columns)}})
                SELECT {{string.Join(", ", columns.Select(column => $"s.\"{column}\""))}}
                FROM "Seed_{{table}}" s
                WHERE NOT EXISTS (
                    SELECT 1 FROM "{{table}}" existing
                    WHERE existing."{{key}}" = s."{{key}}" AND NOT existing."IsDeleted"
                )
                RETURNING "Id"
            )
            INSERT INTO "{{Ledger}}" ("Entity", "Id")
            SELECT '{{table}}', "Id" FROM inserted;

            CREATE TEMP TABLE "Seed_Map_{{table}}" ON COMMIT DROP AS
            SELECT s."Id" AS "SeedId", existing."Id" AS "ActualId"
            FROM "Seed_{{table}}" s
            JOIN "{{table}}" existing ON existing."{{key}}" = s."{{key}}" AND NOT existing."IsDeleted";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF to_regclass('"__SeedCurrentCustomersAndCommunities"') IS NULL THEN
                    RAISE EXCEPTION 'Cannot safely revert seed data: ownership ledger is missing.';
                END IF;
            END $$;

            DELETE FROM "CommunityParticipations" p USING "__SeedCurrentCustomersAndCommunities" s
            WHERE s."Entity" = 'CommunityParticipations' AND p."Id" = s."Id";
            DELETE FROM "CustomerMeteringPoints" p USING "__SeedCurrentCustomersAndCommunities" s
            WHERE s."Entity" = 'CustomerMeteringPoints'
              AND p."CustomerId" = s."Id" AND p."MeteringPointId" = s."RelatedId";

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM "CustomerMeteringPoints" p
                    JOIN "__SeedCurrentCustomersAndCommunities" s
                      ON (s."Entity" = 'Customers' AND s."Id" = p."CustomerId")
                      OR (s."Entity" = 'MeteringPoints' AND s."Id" = p."MeteringPointId")
                ) THEN
                    RAISE EXCEPTION 'Cannot revert seed data: seeded records have additional customer assignments.';
                END IF;
            END $$;

            DELETE FROM "MeteringPoints" p USING "__SeedCurrentCustomersAndCommunities" s
            WHERE s."Entity" = 'MeteringPoints' AND p."Id" = s."Id";
            DELETE FROM "Customers" p USING "__SeedCurrentCustomersAndCommunities" s
            WHERE s."Entity" = 'Customers' AND p."Id" = s."Id";
            DELETE FROM "EnergyCommunities" p USING "__SeedCurrentCustomersAndCommunities" s
            WHERE s."Entity" = 'EnergyCommunities' AND p."Id" = s."Id";
            DROP TABLE "__SeedCurrentCustomersAndCommunities";
            """);
    }

    private static string Columns(string[] columns) => string.Join(", ", columns.Select(column => $"\"{column}\""));

    private static string Literal(object? value) => value switch
    {
        null => "NULL",
        Guid id => $"'{id}'::uuid",
        DateTimeOffset timestamp => $"TIMESTAMPTZ '{timestamp.ToString("O", CultureInfo.InvariantCulture)}'",
        string text => "'" + text.Replace("'", "''") + "'",
        bool boolean => boolean ? "TRUE" : "FALSE",
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"Unsupported seed value type: {value.GetType().Name}.")
    };
}
