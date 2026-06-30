using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyExternalCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FamilyExternalCalendars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FamilyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ICalUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncError = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CachedEventsJson = table.Column<string>(type: "text", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RecordChange = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyExternalCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyExternalCalendars_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyExternalCalendars_FamilyId",
                table: "FamilyExternalCalendars",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyExternalCalendars_FamilyId_IsEnabled",
                table: "FamilyExternalCalendars",
                columns: new[] { "FamilyId", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyExternalCalendars");
        }
    }
}
