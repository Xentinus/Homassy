using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyToProductItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Currency",
                table: "ProductItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Price",
                table: "ProductItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchasedQuantity",
                table: "ProductItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "ProductItems");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "ProductItems");

            migrationBuilder.DropColumn(
                name: "PurchasedQuantity",
                table: "ProductItems");
        }
    }
}
