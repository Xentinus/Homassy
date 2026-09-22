using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homassy.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersonalCalendarNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CalendarNotes_CreatedByUserId",
                table: "CalendarNotes");

            migrationBuilder.AlterColumn<int>(
                name: "FamilyId",
                table: "CalendarNotes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarNotes_CreatedByUserId_Date",
                table: "CalendarNotes",
                columns: new[] { "CreatedByUserId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CalendarNotes_CreatedByUserId_Date",
                table: "CalendarNotes");

            migrationBuilder.AlterColumn<int>(
                name: "FamilyId",
                table: "CalendarNotes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarNotes_CreatedByUserId",
                table: "CalendarNotes",
                column: "CreatedByUserId");
        }
    }
}
