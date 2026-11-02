using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EGWNCentral.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShareMeteringPointsBetweenCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeteringPoints_Customers_CustomerId",
                table: "MeteringPoints");

            migrationBuilder.DropIndex(
                name: "IX_MeteringPoints_CustomerId",
                table: "MeteringPoints");

            migrationBuilder.CreateTable(
                name: "CustomerMeteringPoints",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeteringPointId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerMeteringPoints", x => new { x.CustomerId, x.MeteringPointId });
                    table.ForeignKey(
                        name: "FK_CustomerMeteringPoints_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerMeteringPoints_MeteringPoints_MeteringPointId",
                        column: x => x.MeteringPointId,
                        principalTable: "MeteringPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "CustomerMeteringPoints" ("CustomerId", "MeteringPointId")
                SELECT "CustomerId", "Id" FROM "MeteringPoints";
                """);

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "MeteringPoints");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMeteringPoints_MeteringPointId",
                table: "CustomerMeteringPoints",
                column: "MeteringPointId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "MeteringPoints" p
                        LEFT JOIN "CustomerMeteringPoints" c ON c."MeteringPointId" = p."Id"
                        GROUP BY p."Id" HAVING COUNT(c."CustomerId") <> 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot revert: every metering point must have exactly one customer.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "MeteringPoints",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "MeteringPoints" p SET "CustomerId" = c."CustomerId"
                FROM "CustomerMeteringPoints" c WHERE c."MeteringPointId" = p."Id";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "MeteringPoints",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "CustomerMeteringPoints");

            migrationBuilder.CreateIndex(
                name: "IX_MeteringPoints_CustomerId",
                table: "MeteringPoints",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_MeteringPoints_Customers_CustomerId",
                table: "MeteringPoints",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
