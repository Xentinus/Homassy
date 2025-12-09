using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class RefactShoppingList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ShoppingListItems");

            migrationBuilder.AddColumn<int>(
                name: "ShoppingListId",
                table: "ShoppingListItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ShoppingLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FamilyId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RecordChange = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingLists", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_ShoppingListId",
                table: "ShoppingListItems",
                column: "ShoppingListId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_ShoppingLists_ShoppingListId",
                table: "ShoppingListItems",
                column: "ShoppingListId",
                principalTable: "ShoppingLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_ShoppingLists_ShoppingListId",
                table: "ShoppingListItems");

            migrationBuilder.DropTable(
                name: "ShoppingLists");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingListItems_ShoppingListId",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "ShoppingListId",
                table: "ShoppingListItems");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ShoppingListItems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "FamilyId",
                table: "ShoppingListItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ShoppingListItems",
                type: "integer",
                nullable: true);
        }
    }
}
