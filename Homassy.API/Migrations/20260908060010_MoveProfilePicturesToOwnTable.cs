using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <summary>
    /// Moves avatar bytes off <c>UserProfiles</c> into <c>UserProfilePictures</c>, leaving only a
    /// content-hash version behind.
    /// </summary>
    /// <remarks>
    /// The statement order matters and is not what the scaffolder produced: the table has to exist
    /// and be filled before the old column can be dropped, or the data is gone.
    /// </remarks>
    public partial class MoveProfilePicturesToOwnTable : Migration
    {
        /// <summary>
        /// Strips a <c>data:image/...;base64,</c> prefix and all whitespace off the stored string.
        /// </summary>
        /// <remarks>
        /// Both shapes exist in the column: the processed upload path stored bare base64, an
        /// earlier one stored whatever the client sent.
        /// </remarks>
        private const string CleanedBase64 =
            "regexp_replace(regexp_replace(\"ProfilePictureBase64\", '^data:[^,]*,', ''), '\\s', '', 'g')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfilePictureVersion",
                table: "UserProfiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserProfilePictures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_UserProfilePictures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfilePictures_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfilePictures_UserId",
                table: "UserProfilePictures",
                column: "UserId",
                unique: true);

            // Width/Height stay 0 and ThumbnailData stays null for migrated rows: neither is read
            // when serving, and the image endpoint generates the missing thumbnail the first time
            // one is asked for. The format is sniffed from the magic bytes so the response can
            // carry a truthful Content-Type; anything unrecognised is called JPEG, which is what
            // the upload path produced.
            // The filter is what makes decode() safe — a row that is not decodable base64 would
            // abort the whole migration, and it could not have been rendered by the old code
            // either.
            migrationBuilder.Sql($@"
                WITH decoded AS MATERIALIZED (
                    SELECT ""UserId"", decode({CleanedBase64}, 'base64') AS bytes
                    FROM ""UserProfiles""
                    WHERE ""ProfilePictureBase64"" IS NOT NULL
                      AND {CleanedBase64} ~ '^[A-Za-z0-9+/]+={{0,2}}$'
                      AND length({CleanedBase64}) % 4 = 0
                )
                INSERT INTO ""UserProfilePictures""
                    (""UserId"", ""Data"", ""Format"", ""Width"", ""Height"", ""ThumbnailData"", ""ThumbnailFormat"", ""Version"", ""UpdatedAt"")
                SELECT
                    d.""UserId"",
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
                UPDATE ""UserProfiles"" p
                SET ""ProfilePictureVersion"" = pic.""Version""
                FROM ""UserProfilePictures"" pic
                WHERE pic.""UserId"" = p.""UserId"";
            ");

            migrationBuilder.DropColumn(
                name: "ProfilePictureBase64",
                table: "UserProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfilePictureBase64",
                table: "UserProfiles",
                type: "text",
                nullable: true);

            // encode() wraps at 76 characters; Convert.FromBase64String ignores whitespace, so the
            // round trip is lossless for the code this reverts to.
            migrationBuilder.Sql(@"
                UPDATE ""UserProfiles"" p
                SET ""ProfilePictureBase64"" = encode(pic.""Data"", 'base64')
                FROM ""UserProfilePictures"" pic
                WHERE pic.""UserId"" = p.""UserId"";
            ");

            migrationBuilder.DropTable(
                name: "UserProfilePictures");

            migrationBuilder.DropColumn(
                name: "ProfilePictureVersion",
                table: "UserProfiles");
        }
    }
}
