using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EGWNCentral.Infrastructure.Persistence.Migrations
{
    public partial class AddCustomerNumberSequence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE SEQUENCE IF NOT EXISTS customer_number_seq START WITH 1 INCREMENT BY 1;");
            migrationBuilder.Sql(@"SELECT setval('customer_number_seq', COALESCE((SELECT MAX(CAST(SUBSTRING(""CustomerNumber"" FROM 6) AS INTEGER)) FROM ""Customers"" WHERE ""CustomerNumber"" LIKE 'EGWN-%'), 0), true);");
            migrationBuilder.AlterColumn<string>(
                name: "CustomerNumber",
                table: "Customers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValueSql: "'EGWN-' || lpad(nextval('customer_number_seq')::text, 2, '0')",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldDefaultValueSql: null);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CustomerNumber",
                table: "Customers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldDefaultValueSql: "'EGWN-' || lpad(nextval('customer_number_seq')::text, 2, '0')");

            migrationBuilder.Sql(@"DROP SEQUENCE IF EXISTS customer_number_seq;");
        }
    }
}
