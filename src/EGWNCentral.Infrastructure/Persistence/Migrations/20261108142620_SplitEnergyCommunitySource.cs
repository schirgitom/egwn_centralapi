using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EGWNCentral.Infrastructure.Persistence.Migrations
{
    public partial class SplitEnergyCommunitySource : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Quelle",
                table: "EnergyCommunities",
                newName: "SourceId");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "EnergyCommunities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "EnergyCommunities");

            migrationBuilder.RenameColumn(
                name: "SourceId",
                table: "EnergyCommunities",
                newName: "Quelle");
        }
    }
}
