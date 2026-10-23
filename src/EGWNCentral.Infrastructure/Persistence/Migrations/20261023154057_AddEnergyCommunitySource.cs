using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EGWNCentral.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnergyCommunitySource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Quelle",
                table: "EnergyCommunities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "EnergyCommunities",
                keyColumn: "Id",
                keyValue: new Guid("23ac2aa2-5adf-506e-dae0-3fc21c74093b"),
                column: "Quelle",
                value: null);

            migrationBuilder.UpdateData(
                table: "EnergyCommunities",
                keyColumn: "Id",
                keyValue: new Guid("3566c1e7-22af-bd37-a161-ea3e8cfde2b0"),
                column: "Quelle",
                value: null);

            migrationBuilder.UpdateData(
                table: "EnergyCommunities",
                keyColumn: "Id",
                keyValue: new Guid("38f04c4c-d016-f669-48b1-46ef7eb80fcc"),
                column: "Quelle",
                value: null);

            migrationBuilder.UpdateData(
                table: "EnergyCommunities",
                keyColumn: "Id",
                keyValue: new Guid("484a5236-e5c5-4ef6-43f8-af35dca334eb"),
                column: "Quelle",
                value: null);

            migrationBuilder.UpdateData(
                table: "EnergyCommunities",
                keyColumn: "Id",
                keyValue: new Guid("61deb0b8-235b-7d8f-a829-f10fcacd5ff4"),
                column: "Quelle",
                value: null);

            migrationBuilder.UpdateData(
                table: "EnergyCommunities",
                keyColumn: "Id",
                keyValue: new Guid("694a671d-50fd-02be-3fcb-24fc4cfe6e01"),
                column: "Quelle",
                value: null);

            migrationBuilder.UpdateData(
                table: "EnergyCommunities",
                keyColumn: "Id",
                keyValue: new Guid("7234ebcb-be5a-e4ea-4c04-81c63f650599"),
                column: "Quelle",
                value: null);

            migrationBuilder.UpdateData(
                table: "EnergyCommunities",
                keyColumn: "Id",
                keyValue: new Guid("9573c7ac-9fb8-db2c-b2b3-4ed041ab30dc"),
                column: "Quelle",
                value: null);

            migrationBuilder.UpdateData(
                table: "EnergyCommunities",
                keyColumn: "Id",
                keyValue: new Guid("b84956ce-d353-1d6f-2c94-e4268ce43985"),
                column: "Quelle",
                value: null);

            migrationBuilder.UpdateData(
                table: "EnergyCommunities",
                keyColumn: "Id",
                keyValue: new Guid("c2a896b5-3f8a-9c08-33f4-7f11eaf41659"),
                column: "Quelle",
                value: null);

            migrationBuilder.UpdateData(
                table: "EnergyCommunities",
                keyColumn: "Id",
                keyValue: new Guid("dc200a26-2dba-21c5-6bba-304b2a03df75"),
                column: "Quelle",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Quelle",
                table: "EnergyCommunities");
        }
    }
}
