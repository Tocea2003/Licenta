using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TursibBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddVehiclePositionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehiclePositionHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<string>(type: "text", nullable: false),
                    RouteId = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Speed = table.Column<double>(type: "double precision", nullable: false),
                    Heading = table.Column<double>(type: "double precision", nullable: false),
                    Occupancy = table.Column<int>(type: "integer", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehiclePositionHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePositionHistory_RecordedAt",
                table: "VehiclePositionHistory",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePositionHistory_RouteId_RecordedAt",
                table: "VehiclePositionHistory",
                columns: new[] { "RouteId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePositionHistory_VehicleId_RecordedAt",
                table: "VehiclePositionHistory",
                columns: new[] { "VehicleId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehiclePositionHistory");
        }
    }
}
