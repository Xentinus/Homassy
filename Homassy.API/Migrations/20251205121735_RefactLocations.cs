using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class RefactLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "StorageLocations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ShoppingLocations");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "StorageLocations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "StorageLocations",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFreezer",
                table: "StorageLocations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ShoppingLocations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "ShoppingLocations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "ShoppingLocations",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpeningHours",
                table: "ShoppingLocations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "ShoppingLocations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPurchaseInfos_ShoppingLocationId",
                table: "ProductPurchaseInfos",
                column: "ShoppingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductInventoryItems_StorageLocationId",
                table: "ProductInventoryItems",
                column: "StorageLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductInventoryItems_StorageLocations_StorageLocationId",
                table: "ProductInventoryItems",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPurchaseInfos_ShoppingLocations_ShoppingLocationId",
                table: "ProductPurchaseInfos",
                column: "ShoppingLocationId",
                principalTable: "ShoppingLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductInventoryItems_StorageLocations_StorageLocationId",
                table: "ProductInventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductPurchaseInfos_ShoppingLocations_ShoppingLocationId",
                table: "ProductPurchaseInfos");

            migrationBuilder.DropIndex(
                name: "IX_ProductPurchaseInfos_ShoppingLocationId",
                table: "ProductPurchaseInfos");

            migrationBuilder.DropIndex(
                name: "IX_ProductInventoryItems_StorageLocationId",
                table: "ProductInventoryItems");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "StorageLocations");

            migrationBuilder.DropColumn(
                name: "IsFreezer",
                table: "StorageLocations");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "ShoppingLocations");

            migrationBuilder.DropColumn(
                name: "OpeningHours",
                table: "ShoppingLocations");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "ShoppingLocations");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "StorageLocations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "StorageLocations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ShoppingLocations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "ShoppingLocations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ShoppingLocations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
