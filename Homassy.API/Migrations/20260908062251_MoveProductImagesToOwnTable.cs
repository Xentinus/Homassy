using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <summary>
    /// Moves product picture bytes off <c>Products</c> into <c>ProductImages</c>, leaving only a
    /// content-hash version behind. The avatar equivalent is
    /// <c>MoveProfilePicturesToOwnTable</c>; see its notes — the statement order and the base64
    /// filter are the same, and for the same reasons.
    /// </summary>
    public partial class MoveProductImagesToOwnTable : Migration
    {
        private const string CleanedBase64 =
            "regexp_replace(regexp_replace(\"ProductPictureBase64\", '^data:[^,]*,', ''), '\\s', '', 'g')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductPictureVersion",
                table: "Products",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId",
                unique: true);

            migrationBuilder.Sql($@"
                WITH decoded AS MATERIALIZED (
                    SELECT ""Id"" AS product_id, decode({CleanedBase64}, 'base64') AS bytes
                    FROM ""Products""
                    WHERE ""ProductPictureBase64"" IS NOT NULL
                      AND {CleanedBase64} ~ '^[A-Za-z0-9+/]+={{0,2}}$'
                      AND length({CleanedBase64}) % 4 = 0
                )
                INSERT INTO ""ProductImages""
                    (""ProductId"", ""Data"", ""Format"", ""Width"", ""Height"", ""ThumbnailData"", ""ThumbnailFormat"", ""Version"", ""UpdatedAt"")
                SELECT
                    d.product_id,
                    d.bytes,
                    CASE
                        WHEN substring(d.bytes from 1 for 3) = '\xffd8ff'::bytea THEN 1
                        WHEN substring(d.bytes from 1 for 8) = '\x89504e470d0a1a0a'::bytea THEN 2
                        WHEN substring(d.bytes from 1 for 4) = '\x52494646'::bytea
                             AND substring(d.bytes from 9 for 4) = '\x57454250'::bytea THEN 3
                        ELSE 1
                    END,
                    0,
                    0,
                    NULL,
                    0,
                    substr(md5(d.bytes), 1, 16),
                    now()
                FROM decoded d
                WHERE length(d.bytes) > 0;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Products"" p
                SET ""ProductPictureVersion"" = img.""Version""
                FROM ""ProductImages"" img
                WHERE img.""ProductId"" = p.""Id"";
            ");

            migrationBuilder.DropColumn(
                name: "ProductPictureBase64",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductPictureBase64",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""Products"" p
                SET ""ProductPictureBase64"" = encode(img.""Data"", 'base64')
                FROM ""ProductImages"" img
                WHERE img.""ProductId"" = p.""Id"";
            ");

            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropColumn(
                name: "ProductPictureVersion",
                table: "Products");
        }
    }
}
