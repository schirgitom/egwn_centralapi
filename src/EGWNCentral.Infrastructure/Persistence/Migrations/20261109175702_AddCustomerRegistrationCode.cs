using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EGWNCentral.Infrastructure.Persistence.Migrations;

public partial class AddCustomerRegistrationCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RegistrationCode",
            table: "Customers",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValueSql: "upper(replace(gen_random_uuid()::text, '-', ''))");

        migrationBuilder.CreateIndex(
            name: "IX_Customers_RegistrationCode",
            table: "Customers",
            column: "RegistrationCode",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Customers_RegistrationCode",
            table: "Customers");

        migrationBuilder.DropColumn(
            name: "RegistrationCode",
            table: "Customers");
    }
}
