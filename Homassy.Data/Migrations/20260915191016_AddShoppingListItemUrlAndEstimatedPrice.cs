using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homassy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingListItemUrlAndEstimatedPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimatedPriceCurrency",
                table: "ShoppingListItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedUnitPrice",
                table: "ShoppingListItems",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "ShoppingListItems",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedPriceCurrency",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "EstimatedUnitPrice",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "ShoppingListItems");
        }
    }
}
