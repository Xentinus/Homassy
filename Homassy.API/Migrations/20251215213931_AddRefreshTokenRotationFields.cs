using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenRotationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousRefreshToken",
                table: "UserAuthentications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreviousRefreshTokenExpiry",
                table: "UserAuthentications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TokenFamily",
                table: "UserAuthentications",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousRefreshToken",
                table: "UserAuthentications");

            migrationBuilder.DropColumn(
                name: "PreviousRefreshTokenExpiry",
                table: "UserAuthentications");

            migrationBuilder.DropColumn(
                name: "TokenFamily",
                table: "UserAuthentications");
        }
    }
}
