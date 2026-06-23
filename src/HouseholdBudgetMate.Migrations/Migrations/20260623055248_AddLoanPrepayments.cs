using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HouseholdBudgetMate.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanPrepayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoanPrepayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    PrepaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanPrepayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanPrepayments_Loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanPrepayments_LoanId_PrepaymentDate",
                table: "LoanPrepayments",
                columns: new[] { "LoanId", "PrepaymentDate" });

            // Backfill legacy prepayment expense rows only when they identify one loan unambiguously.
            migrationBuilder.Sql("""
                INSERT INTO "LoanPrepayments" ("LoanId", "PrepaymentDate", "Amount", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT
                    matched."LoanId",
                    make_date(matched."Year", matched."Month", 1),
                    matched."ActualAmount",
                    NOW(),
                    NOW()
                FROM (
                    SELECT
                        e."Id" AS "ExpenseId",
                        MAX(l."Id") AS "LoanId",
                        mp."Year",
                        mp."Month",
                        e."ActualAmount",
                        COUNT(*) AS "MatchCount"
                    FROM "Expenses" e
                    INNER JOIN "MonthPlans" mp
                        ON mp."Id" = e."MonthPlanId"
                        AND mp."UserId" = e."UserId"
                    INNER JOIN "Categories" c
                        ON c."Id" = e."CategoryId"
                        AND c."Name" = 'Kredyt'
                    INNER JOIN "Loans" l
                        ON l."UserId" = e."UserId"
                        AND l."Name" = left(e."Name", length(e."Name") - length(' - nadpłata'))
                        AND (
                            (l."TagId" IS NULL AND e."TagId" IS NULL)
                            OR l."TagId" = e."TagId"
                        )
                    WHERE e."LoanInstallmentId" IS NULL
                        AND e."RegularExpenseDefinitionId" IS NULL
                        AND e."ActualAmount" > 0
                        AND e."PlannedAmount" = 0
                        AND e."ShowRemainingInUI" = TRUE
                        AND e."Name" LIKE '% - nadpłata'
                        AND e."IsDeleted" = FALSE
                    GROUP BY e."Id", mp."Year", mp."Month", e."ActualAmount"
                ) matched
                WHERE matched."MatchCount" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanPrepayments");
        }
    }
}
