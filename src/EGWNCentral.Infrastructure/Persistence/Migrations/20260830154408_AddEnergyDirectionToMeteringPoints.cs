using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EGWNCentral.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnergyDirectionToMeteringPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnergyDirection",
                table: "MeteringPoints",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "generation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnergyDirection",
                table: "MeteringPoints");
        }
    }
}
