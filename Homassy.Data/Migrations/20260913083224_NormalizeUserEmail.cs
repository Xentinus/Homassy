using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homassy.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUserEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Two live rows for one address, differing only in case or surrounding whitespace,
            // are two accounts for one person. Canonicalising would make them collide, and this
            // migration must not pick a winner - stop instead, naming the rows to merge.
            //
            // Naming them by Users.Id, never by address: this runs in the deploy job, so the
            // message lands in a CI log, and an email address is the user's personal data. An id
            // points at the same row just as well for whoever has to merge them.
            migrationBuilder.Sql("""
                DO $$
                DECLARE duplicate_ids text;
                DECLARE duplicate_groups int;
                BEGIN
                    SELECT string_agg(d.ids, ' | '), count(*)
                      INTO duplicate_ids, duplicate_groups
                      FROM (
                            SELECT string_agg("Id"::text, ', ' ORDER BY "Id") AS ids
                              FROM "Users"
                             WHERE "IsDeleted" = false
                             GROUP BY lower(btrim("Email"))
                            HAVING count(*) > 1
                           ) d;

                    IF duplicate_groups > 0 THEN
                        RAISE EXCEPTION
                            'Users.Email cannot be made unique: % address(es) are held by more than one live user, differing only in case or whitespace. Merge these Users.Id groups and run the migration again: %',
                            duplicate_groups,
                            duplicate_ids;
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
