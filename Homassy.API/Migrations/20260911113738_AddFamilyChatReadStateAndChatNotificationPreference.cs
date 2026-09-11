using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyChatReadStateAndChatNotificationPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfilled to ON for every existing row, the same way #116's in-app flag was. EF
            // scaffolds `false` here because that is the CLR default; it is not the entity's
            // default (`= true`), and shipping false would silently mute the chat for every user
            // who already exists - a preference they were never shown a control for.
            migrationBuilder.AddColumn<bool>(
                name: "PushFamilyChatEnabled",
                table: "UserNotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "FamilyChatReadStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FamilyId = table.Column<int>(type: "integer", nullable: false),
                    LastReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RecordChange = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyChatReadStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyChatReadStates_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FamilyChatReadStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyChatReadStates_FamilyId",
                table: "FamilyChatReadStates",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyChatReadStates_UserId_FamilyId",
                table: "FamilyChatReadStates",
                columns: new[] { "UserId", "FamilyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyChatReadStates");

            migrationBuilder.DropColumn(
                name: "PushFamilyChatEnabled",
                table: "UserNotificationPreferences");
        }
    }
}
