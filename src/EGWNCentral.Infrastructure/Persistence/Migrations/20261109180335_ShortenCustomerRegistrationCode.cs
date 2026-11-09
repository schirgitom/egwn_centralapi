using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EGWNCentral.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShortenCustomerRegistrationCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION generate_customer_registration_code() RETURNS text
                LANGUAGE plpgsql VOLATILE AS $$
                DECLARE
                    alphabet constant text := 'ABCDEFGHIJKLMNPQRSTUVWXYZ123456789';
                    candidate text;
                    random_byte integer;
                BEGIN
                    PERFORM pg_advisory_xact_lock(917403625);
                    FOR attempt IN 1..100 LOOP
                        candidate := '';
                        WHILE length(candidate) < 6 LOOP
                            random_byte := get_byte(uuid_send(gen_random_uuid()), 0);
                            IF random_byte < 238 THEN
                                candidate := candidate || substr(alphabet, (random_byte % 34) + 1, 1);
                            END IF;
                        END LOOP;
                        IF NOT EXISTS (SELECT 1 FROM "Customers" WHERE "RegistrationCode" = candidate) THEN
                            RETURN candidate;
                        END IF;
                    END LOOP;
                    RAISE EXCEPTION 'Unable to generate a unique registration code after 100 attempts.';
                END;
                $$;

                LOCK TABLE "Customers" IN ACCESS EXCLUSIVE MODE;
                DO $$
                DECLARE customer_id uuid;
                BEGIN
                    FOR customer_id IN
                        SELECT "Id" FROM "Customers" WHERE "RegistrationCode" !~ '^[A-NP-Z1-9]{6}$'
                    LOOP
                        UPDATE "Customers"
                        SET "RegistrationCode" = generate_customer_registration_code()
                        WHERE "Id" = customer_id;
                    END LOOP;
                END;
                $$;

                UPDATE "CustomerCreatedOutbox" AS message
                SET "Payload" = jsonb_set(message."Payload"::jsonb,
                    '{customer,registrationCode}', to_jsonb(customer."RegistrationCode"))::text
                FROM "Customers" AS customer
                WHERE message."Payload"::jsonb #>> '{customer,id}' = customer."Id"::text;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "RegistrationCode",
                table: "Customers",
                type: "character varying(6)",
                maxLength: 6,
                nullable: false,
                defaultValueSql: "generate_customer_registration_code()",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValueSql: "upper(replace(gen_random_uuid()::text, '-', ''))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Customers_RegistrationCode_Format",
                table: "Customers",
                sql: "\"RegistrationCode\" ~ '^[A-NP-Z1-9]{6}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Customers_RegistrationCode_Format",
                table: "Customers");

            migrationBuilder.AlterColumn<string>(
                name: "RegistrationCode",
                table: "Customers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValueSql: "upper(replace(gen_random_uuid()::text, '-', ''))",
                oldClrType: typeof(string),
                oldType: "character varying(6)",
                oldMaxLength: 6,
                oldDefaultValueSql: "generate_customer_registration_code()");

            migrationBuilder.Sql("DROP FUNCTION generate_customer_registration_code();");
        }
    }
}
