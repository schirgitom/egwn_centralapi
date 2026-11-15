using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EGWNCentral.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKeycloakMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KeycloakMemberships",
                columns: table => new
                {
                    KeycloakUserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeycloakMemberships", x => x.KeycloakUserId);
                    table.ForeignKey(
                        name: "FK_KeycloakMemberships_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KeycloakMemberships_CustomerId",
                table: "KeycloakMemberships",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KeycloakMemberships");
        }
    }
}
