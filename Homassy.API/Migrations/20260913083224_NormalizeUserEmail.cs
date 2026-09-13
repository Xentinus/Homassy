using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUserEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Two live rows for one address, differing only in case or surrounding whitespace,
            // are two accounts for one person. Canonicalising would make them collide, and this
            // migration must not pick a winner - stop with the addresses named instead, so
            // whoever runs the deploy knows exactly which rows to merge.
            migrationBuilder.Sql("""
                DO $$
                DECLARE duplicates text;
                BEGIN
                    SELECT string_agg(d.address, ', ')
                      INTO duplicates
                      FROM (
                            SELECT lower(btrim("Email")) AS address
                              FROM "Users"
                             WHERE "IsDeleted" = false
                             GROUP BY lower(btrim("Email"))
                            HAVING count(*) > 1
                           ) d;

                    IF duplicates IS NOT NULL THEN
                        RAISE EXCEPTION
                            'Users.Email cannot be made unique: % occur more than once, differing only in case or whitespace. Merge those accounts and run the migration again.',
                            duplicates;
                    END IF;
                END $$;
                """);

            // Normalisation used to be applied by the individual write paths, so a row that
            // reached the table another way kept its original casing and could never be found by
            // the lookup, which normalises what it is given. User.Email normalises on the way in
            // now; these are the rows that predate it.
            migrationBuilder.Sql("""
                UPDATE "Users"
                   SET "Email" = lower(btrim("Email"))
                 WHERE "Email" <> lower(btrim("Email"));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");
        }
    }
}
