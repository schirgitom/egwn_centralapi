using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EGWNCentral.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnergyCommunitiesAndParticipations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnergyCommunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnergyCommunities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityParticipations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnergyCommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeteringPointId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipationPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityParticipations", x => x.Id);
                    table.CheckConstraint("CK_CommunityParticipations_ParticipationPercentage", "\"ParticipationPercentage\" >= 0 AND \"ParticipationPercentage\" <= 100");
                    table.ForeignKey(
                        name: "FK_CommunityParticipations_EnergyCommunities_EnergyCommunityId",
                        column: x => x.EnergyCommunityId,
                        principalTable: "EnergyCommunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityParticipations_MeteringPoints_MeteringPointId",
                        column: x => x.MeteringPointId,
                        principalTable: "MeteringPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityParticipations_EnergyCommunityId_MeteringPointId",
                table: "CommunityParticipations",
                columns: new[] { "EnergyCommunityId", "MeteringPointId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityParticipations_MeteringPointId",
                table: "CommunityParticipations",
                column: "MeteringPointId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityParticipations");

            migrationBuilder.DropTable(
                name: "EnergyCommunities");
        }
    }
}
