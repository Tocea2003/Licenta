using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TursibBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddLine2Route : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Routes",
                columns: new[] { "Id", "Name", "RouteNumber" },
                values: new object[] { 3, "Scandia/Neveon - Piața Cibin", "2" });

            migrationBuilder.InsertData(
                table: "Stations",
                columns: new[] { "Id", "Latitude", "Longitude", "Name" },
                values: new object[,]
                {
                    { 30, 45.765012300000002, 24.112345600000001, "Scandia/S.C. Neveon" },
                    { 31, 45.768923399999998, 24.123456699999998, "IRMES" },
                    { 32, 45.783456700000002, 24.138901199999999, "Str. Ștefan cel Mare" },
                    { 33, 45.786788999999999, 24.142345599999999, "Str. Semaforului" },
                    { 34, 45.792345599999997, 24.150123399999998, "B-dul Coposu" },
                    { 35, 45.792610000000003, 24.15137, "Gară CFR Sibiu" },
                    { 36, 45.794567800000003, 24.1534567, "Str. Regele Ferdinand" },
                    { 37, 45.796788999999997, 24.155678900000002, "Str. Abatorului" },
                    { 38, 45.798901200000003, 24.157890099999999, "Târgul Fânului" },
                    { 39, 45.799876500000003, 24.1456789, "Str. Râului" }
                });

            migrationBuilder.InsertData(
                table: "Buses",
                columns: new[] { "Id", "CurrentRouteId", "InternalName", "LicensePlate" },
                values: new object[] { 3, 3, "Bus 102", "SB-02-DEF" });

            migrationBuilder.InsertData(
                table: "RouteStations",
                columns: new[] { "Id", "Order", "RouteId", "StationId" },
                values: new object[,]
                {
                    { 31, 1, 3, 30 },
                    { 32, 2, 3, 31 },
                    { 33, 3, 3, 32 },
                    { 34, 4, 3, 33 },
                    { 35, 5, 3, 27 },
                    { 36, 6, 3, 8 },
                    { 37, 7, 3, 34 },
                    { 38, 8, 3, 35 },
                    { 39, 9, 3, 36 },
                    { 40, 10, 3, 37 },
                    { 41, 11, 3, 38 },
                    { 42, 12, 3, 39 },
                    { 43, 13, 3, 12 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Buses",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "RouteStations",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "RouteStations",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "RouteStations",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "RouteStations",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "RouteStations",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "RouteStations",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "RouteStations",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "RouteStations",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "RouteStations",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "RouteStations",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "RouteStations",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "RouteStations",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "RouteStations",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "Routes",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Stations",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Stations",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Stations",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "Stations",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Stations",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Stations",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "Stations",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "Stations",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "Stations",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "Stations",
                keyColumn: "Id",
                keyValue: 39);
        }
    }
}
