using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Homassy.API.Migrations
{
    /// <inheritdoc />
    public partial class AddItemAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemAutomations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductInventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    FamilyId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    ScheduleType = table.Column<int>(type: "integer", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: true),
                    ScheduledDayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    ScheduledDayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    ScheduledTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    ConsumeQuantity = table.Column<decimal>(type: "numeric", nullable: true),
                    ConsumeUnit = table.Column<int>(type: "integer", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    NextExecutionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RecordChange = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemAutomations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemAutomations_ProductInventoryItems_ProductInventoryItemId",
                        column: x => x.ProductInventoryItemId,
                        principalTable: "ProductInventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemAutomationExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemAutomationId = table.Column<int>(type: "integer", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConsumedQuantity = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TriggeredByUserId = table.Column<int>(type: "integer", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RecordChange = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemAutomationExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemAutomationExecutions_ItemAutomations_ItemAutomationId",
                        column: x => x.ItemAutomationId,
                        principalTable: "ItemAutomations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemAutomationExecutions_ExecutedAt",
                table: "ItemAutomationExecutions",
                column: "ExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ItemAutomationExecutions_ItemAutomationId",
                table: "ItemAutomationExecutions",
                column: "ItemAutomationId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemAutomations_IsEnabled_NextExecutionAt",
                table: "ItemAutomations",
                columns: new[] { "IsEnabled", "NextExecutionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemAutomations_ProductInventoryItemId",
                table: "ItemAutomations",
                column: "ProductInventoryItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemAutomationExecutions");

            migrationBuilder.DropTable(
                name: "ItemAutomations");
        }
    }
}
