using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class ShoppingLocationGoogleMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpeningHours",
                table: "ShoppingLocations");

            migrationBuilder.AddColumn<string>(
                name: "GoogleMaps",
                table: "ShoppingLocations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleMaps",
                table: "ShoppingLocations");

            migrationBuilder.AddColumn<string>(
                name: "OpeningHours",
                table: "ShoppingLocations",
                type: "jsonb",
                nullable: true);
        }
    }
}
