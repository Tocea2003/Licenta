using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TursibBackend.Migrations
{
    /// <inheritdoc />
    public partial class InitialWithPreciseCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RouteNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", precision: 18, scale: 10, nullable: false),
                    Longitude = table.Column<double>(type: "REAL", precision: 18, scale: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Buses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LicensePlate = table.Column<string>(type: "TEXT", nullable: false),
                    InternalName = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentRouteId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Buses_Routes_CurrentRouteId",
                        column: x => x.CurrentRouteId,
                        principalTable: "Routes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RouteStations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RouteId = table.Column<int>(type: "INTEGER", nullable: false),
                    StationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteStations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RouteStations_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteStations_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Routes",
                columns: new[] { "Id", "Name", "RouteNumber" },
                values: new object[,]
                {
                    { 1, "Cimitir - Hornbach", "1" },
                    { 2, "Kaufland Arhitecților - Continental", "11" }
                });

            migrationBuilder.InsertData(
                table: "Stations",
                columns: new[] { "Id", "Latitude", "Longitude", "Name" },
                values: new object[,]
                {
                    { 1, 45.767937199999999, 24.138513, "Cimitir" },
                    { 2, 45.770434199999997, 24.138480999999999, "Calea Dumbrăvii (Cimitir)" },
                    { 3, 45.772008800000002, 24.140490700000001, "Str. Siretului" },
                    { 4, 45.774553900000001, 24.1485366, "Calea Cisnădiei" },
                    { 5, 45.777395499999997, 24.157934999999998, "Str. Rahovei (Piața Rahova)" },
                    { 6, 45.779129599999997, 24.155641800000001, "B-dul M. Viteazu" },
                    { 7, 45.781767199999997, 24.149777199999999, "Calea Dumbrăvii (Viteazu)" },
                    { 8, 45.790972400000001, 24.148515199999999, "Piața Unirii" },
                    { 9, 45.792625899999997, 24.1408296, "Str. A. Șaguna" },
                    { 10, 45.792625899999997, 24.1408296, "Șos. Alba Iulia" },
                    { 11, 45.7961989, 24.139075699999999, "Str. Malului" },
                    { 12, 45.798989400000004, 24.144453899999998, "Piața Cibin" },
                    { 13, 45.799472999999999, 24.1442871, "Str. Cibinului" },
                    { 14, 45.808641700000003, 24.1495909, "Str. Rusciorului" },
                    { 15, 45.807449599999998, 24.1458732, "Str. Lungă" },
                    { 16, 45.811254400000003, 24.148464000000001, "Calea Șurii Mari (Halta Sibiu)" },
                    { 17, 45.821075399999998, 24.1491845, "Hornbach" },
                    { 20, 45.770613599999997, 24.146665599999999, "Kaufland Arhitecților" },
                    { 21, 45.772673699999999, 24.1481128, "C. Cisnădiei - Primăverii" },
                    { 22, 45.754687599999997, 24.138710199999998, "Cartierul Primăverii" },
                    { 23, 45.777395499999997, 24.157934999999998, "Str. Rahova" },
                    { 24, 45.778119699999998, 24.149690799999998, "B-dul M. Viteazu (Iorga)" },
                    { 25, 45.780909999999999, 24.158085, "Str. N. Iorga / Cedonia" },
                    { 26, 45.782547200000003, 24.162478700000001, "Str. Luptei" },
                    { 27, 45.788394400000001, 24.151538299999999, "B-dul V. Milea" },
                    { 28, 45.796409099999998, 24.078269800000001, "Str. Salzburg" },
                    { 29, 45.797894700000001, 24.077089300000001, "S.C. Continental" }
                });

            migrationBuilder.InsertData(
                table: "Buses",
                columns: new[] { "Id", "CurrentRouteId", "InternalName", "LicensePlate" },
                values: new object[,]
                {
                    { 1, 1, "Bus 101", "SB-01-ABC" },
                    { 2, 2, "Bus 211", "SB-11-XYZ" }
                });

            migrationBuilder.InsertData(
                table: "RouteStations",
                columns: new[] { "Id", "Order", "RouteId", "StationId" },
                values: new object[,]
                {
                    { 1, 1, 1, 1 },
                    { 2, 2, 1, 2 },
                    { 3, 3, 1, 3 },
                    { 4, 4, 1, 4 },
                    { 5, 5, 1, 5 },
                    { 6, 6, 1, 6 },
                    { 7, 7, 1, 7 },
                    { 8, 8, 1, 8 },
                    { 9, 9, 1, 9 },
                    { 10, 10, 1, 10 },
                    { 11, 11, 1, 11 },
                    { 12, 12, 1, 12 },
                    { 13, 13, 1, 13 },
                    { 14, 14, 1, 14 },
                    { 15, 15, 1, 15 },
                    { 16, 16, 1, 16 },
                    { 17, 17, 1, 17 },
                    { 18, 1, 2, 20 },
                    { 19, 2, 2, 21 },
                    { 20, 3, 2, 22 },
                    { 21, 4, 2, 4 },
                    { 22, 5, 2, 23 },
                    { 23, 6, 2, 24 },
                    { 24, 7, 2, 25 },
                    { 25, 8, 2, 26 },
                    { 26, 9, 2, 27 },
                    { 27, 10, 2, 8 },
                    { 28, 11, 2, 9 },
                    { 29, 12, 2, 28 },
                    { 30, 13, 2, 29 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Buses_CurrentRouteId",
                table: "Buses",
                column: "CurrentRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStations_RouteId",
                table: "RouteStations",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStations_StationId",
                table: "RouteStations",
                column: "StationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Buses");

            migrationBuilder.DropTable(
                name: "RouteStations");

            migrationBuilder.DropTable(
                name: "Routes");

            migrationBuilder.DropTable(
                name: "Stations");
        }
    }
}
