using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TursibBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoritesToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Favorites",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Favorites",
                table: "Users");
        }
    }
}
