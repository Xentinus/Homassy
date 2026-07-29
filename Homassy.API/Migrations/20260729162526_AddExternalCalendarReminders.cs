using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalCalendarReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "AllDayNotifyTime",
                table: "FamilyExternalCalendars",
                type: "time without time zone",
                nullable: false,
                // Existing rows must match the entity default (08:00), not midnight — midnight is the very
                // thing the all-day notify time exists to avoid.
                defaultValue: new TimeOnly(8, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "ReminderLeadTimesJson",
                table: "FamilyExternalCalendars",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalCalendarReminderDispatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExternalCalendarId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    EventUid = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OccurrenceKey = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LeadTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalCalendarReminderDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalCalendarReminderDispatches_FamilyExternalCalendars_~",
                        column: x => x.ExternalCalendarId,
                        principalTable: "FamilyExternalCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalCalendarReminderDispatches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExtCalReminderDispatches_Occurrence",
                table: "ExternalCalendarReminderDispatches",
                columns: new[] { "ExternalCalendarId", "UserId", "EventUid", "OccurrenceKey", "LeadTimeMinutes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExtCalReminderDispatches_SentAt",
                table: "ExternalCalendarReminderDispatches",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCalendarReminderDispatches_UserId",
                table: "ExternalCalendarReminderDispatches",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalCalendarReminderDispatches");

            migrationBuilder.DropColumn(
                name: "AllDayNotifyTime",
                table: "FamilyExternalCalendars");

            migrationBuilder.DropColumn(
                name: "ReminderLeadTimesJson",
                table: "FamilyExternalCalendars");
        }
    }
}
