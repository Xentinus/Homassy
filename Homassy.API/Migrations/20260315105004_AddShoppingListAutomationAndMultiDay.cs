using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingListAutomationAndMultiDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItemAutomations_ProductInventoryItems_ProductInventoryItemId",
                table: "ItemAutomations");

            migrationBuilder.AlterColumn<int>(
                name: "ProductInventoryItemId",
                table: "ItemAutomations",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<decimal>(
                name: "AddQuantity",
                table: "ItemAutomations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AddUnit",
                table: "ItemAutomations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "ItemAutomations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShoppingListId",
                table: "ItemAutomations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduledDaysOfWeek",
                table: "ItemAutomations",
                type: "integer",
                nullable: true);

            // Data migration: convert ScheduledDayOfWeek (System.DayOfWeek) to ScheduledDaysOfWeek (DaysOfWeek flags)
            // System.DayOfWeek: Sunday=0, Monday=1, Tuesday=2, Wednesday=3, Thursday=4, Friday=5, Saturday=6
            // DaysOfWeek flags: Monday=1, Tuesday=2, Wednesday=4, Thursday=8, Friday=16, Saturday=32, Sunday=64
            migrationBuilder.Sql(@"
                UPDATE ""ItemAutomations""
                SET ""ScheduledDaysOfWeek"" = CASE ""ScheduledDayOfWeek""
                    WHEN 0 THEN 64   -- Sunday
                    WHEN 1 THEN 1    -- Monday
                    WHEN 2 THEN 2    -- Tuesday
                    WHEN 3 THEN 4    -- Wednesday
                    WHEN 4 THEN 8    -- Thursday
                    WHEN 5 THEN 16   -- Friday
                    WHEN 6 THEN 32   -- Saturday
                    ELSE NULL
                END
                WHERE ""ScheduledDayOfWeek"" IS NOT NULL;
            ");

            migrationBuilder.DropColumn(
                name: "ScheduledDayOfWeek",
                table: "ItemAutomations");

            migrationBuilder.CreateIndex(
                name: "IX_ItemAutomations_ProductId",
                table: "ItemAutomations",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemAutomations_ShoppingListId",
                table: "ItemAutomations",
                column: "ShoppingListId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemAutomations_ProductInventoryItems_ProductInventoryItemId",
                table: "ItemAutomations",
                column: "ProductInventoryItemId",
                principalTable: "ProductInventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemAutomations_Products_ProductId",
                table: "ItemAutomations",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemAutomations_ShoppingLists_ShoppingListId",
                table: "ItemAutomations",
                column: "ShoppingListId",
                principalTable: "ShoppingLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItemAutomations_ProductInventoryItems_ProductInventoryItemId",
                table: "ItemAutomations");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemAutomations_Products_ProductId",
                table: "ItemAutomations");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemAutomations_ShoppingLists_ShoppingListId",
                table: "ItemAutomations");

            migrationBuilder.DropIndex(
                name: "IX_ItemAutomations_ProductId",
                table: "ItemAutomations");

            migrationBuilder.DropIndex(
                name: "IX_ItemAutomations_ShoppingListId",
                table: "ItemAutomations");

            migrationBuilder.DropColumn(
                name: "AddQuantity",
                table: "ItemAutomations");

            migrationBuilder.DropColumn(
                name: "AddUnit",
                table: "ItemAutomations");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "ItemAutomations");

            migrationBuilder.DropColumn(
                name: "ScheduledDaysOfWeek",
                table: "ItemAutomations");

            // Restore ScheduledDayOfWeek column (data cannot be fully recovered from flags)
            migrationBuilder.AddColumn<int>(
                name: "ScheduledDayOfWeek",
                table: "ItemAutomations",
                type: "integer",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "ShoppingListId",
                table: "ItemAutomations");

            migrationBuilder.AlterColumn<int>(
                name: "ProductInventoryItemId",
                table: "ItemAutomations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemAutomations_ProductInventoryItems_ProductInventoryItemId",
                table: "ItemAutomations",
                column: "ProductInventoryItemId",
                principalTable: "ProductInventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
