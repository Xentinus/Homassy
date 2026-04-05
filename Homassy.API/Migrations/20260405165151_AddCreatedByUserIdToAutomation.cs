using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByUserIdToAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "ItemAutomations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill existing rows: use UserId if set, otherwise 0
            migrationBuilder.Sql(
                "UPDATE \"ItemAutomations\" SET \"CreatedByUserId\" = COALESCE(\"UserId\", 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "ItemAutomations");
        }
    }
}
