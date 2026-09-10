using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class PurchasePriceToDecimal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Raw SQL, not migrationBuilder.AlterColumn<decimal>(...): the scaffolded AlterColumn
            // lowers to a bare `ALTER COLUMN "Price" TYPE numeric(18,4)` with no USING clause,
            // relying on Postgres's implicit integer->numeric cast to carry existing values across
            // unchanged. That happens to be safe here (numeric(18,4) losslessly represents every
            // int value), but it is Postgres's default, not something this migration states - an
            // explicit USING makes the widening conversion visible in the migration itself instead
            // of resting on a cast catalog lookup.
            migrationBuilder.Sql(@"
                ALTER TABLE ""ProductPurchaseInfos""
                ALTER COLUMN ""Price"" TYPE numeric(18,4)
                USING ""Price""::numeric;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mirrors Up(): explicit USING, this time because it is not optional. Postgres has no
            // implicit numeric->integer cast (a numeric(18,4) value can carry fractional cents an
            // integer cannot), so ROUND(...) states outright that reverting this migration is lossy
            // - a 12.99 purchase becomes 13, never silently 12 via truncation.
            migrationBuilder.Sql(@"
                ALTER TABLE ""ProductPurchaseInfos""
                ALTER COLUMN ""Price"" TYPE integer
                USING ROUND(""Price"")::integer;
            ");
        }
    }
}
