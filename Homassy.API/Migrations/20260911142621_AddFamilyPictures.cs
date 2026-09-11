using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyPictures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The family picture moves out of a base64 column on Families and into its own
            // StoredImageEntity table, the way avatars, product images and chat pictures already
            // live. The column is dropped rather than migrated: the bytes it held never went
            // through IImageProcessingService (no format, no dimensions, no thumbnail), so there
            // is nothing SQL alone could turn into a FamilyPictures row. Nothing in the app ever
            // wrote it - there was no upload UI - so what is dropped is an empty column.
            migrationBuilder.DropColumn(
                name: "FamilyPictureBase64",
                table: "Families");

            migrationBuilder.AddColumn<string>(
                name: "FamilyPictureVersion",
                table: "Families",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FamilyPictures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FamilyId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_FamilyPictures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyPictures_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyPictures_FamilyId",
                table: "FamilyPictures",
                column: "FamilyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyPictures");

            migrationBuilder.DropColumn(
                name: "FamilyPictureVersion",
                table: "Families");

            migrationBuilder.AddColumn<string>(
                name: "FamilyPictureBase64",
                table: "Families",
                type: "text",
                nullable: true);
        }
    }
}
