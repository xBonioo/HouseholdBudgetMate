using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseholdBudgetMate.Migrations.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260601090000_AddAccountActiveFrom")]
    public partial class AddAccountActiveFrom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActiveFromUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Accounts" AS account
                SET "ActiveFromUtc" = restore."ChangedAtUtc"
                FROM (
                    SELECT "EntityId", MAX("ChangedAtUtc") AS "ChangedAtUtc"
                    FROM "AuditLogs"
                    WHERE "EntityType" = 'Account'
                      AND "Operation" = 'Update'
                      AND COALESCE("OldValuesJson" ->> 'IsArchived', "OldValuesJson" ->> 'isArchived') = 'true'
                      AND COALESCE("NewValuesJson" ->> 'IsArchived', "NewValuesJson" ->> 'isArchived') = 'false'
                    GROUP BY "EntityId"
                ) AS restore
                WHERE account."Id" = restore."EntityId"
                  AND account."IsArchived" = false
                  AND account."ActiveFromUtc" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveFromUtc",
                table: "Accounts");
        }
    }
}
