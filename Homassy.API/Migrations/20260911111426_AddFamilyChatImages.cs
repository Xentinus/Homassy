using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyChatImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FamilyChatImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FamilyChatMessageId = table.Column<int>(type: "integer", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    Format = table.Column<int>(type: "integer", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    ThumbnailData = table.Column<byte[]>(type: "bytea", nullable: true),
                    ThumbnailFormat = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyChatImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyChatImages_FamilyChatMessages_FamilyChatMessageId",
                        column: x => x.FamilyChatMessageId,
                        principalTable: "FamilyChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyChatImages_FamilyChatMessageId",
                table: "FamilyChatImages",
                column: "FamilyChatMessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyChatImages");
        }
    }
}
