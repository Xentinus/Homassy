using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FamilyChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FamilyId = table.Column<int>(type: "integer", nullable: false),
                    SenderUserId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RecordChange = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyChatMessages_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FamilyChatMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyChatMessages_FamilyId_SentAt",
                table: "FamilyChatMessages",
                columns: new[] { "FamilyId", "SentAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyChatMessages_SenderUserId",
                table: "FamilyChatMessages",
                column: "SenderUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyChatMessages");
        }
    }
}
