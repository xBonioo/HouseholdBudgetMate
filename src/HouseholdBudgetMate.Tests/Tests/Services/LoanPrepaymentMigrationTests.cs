using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class LoanPrepaymentMigrationTests
{
    [Fact]
    public void AddLoanPrepayments_Should_Backfill_Only_Unambiguous_Legacy_Prepayment_Expenses()
    {
        var migration = ReadRepoFile(
            "src/HouseholdBudgetMate.Migrations/Migrations/20260623055248_AddLoanPrepayments.cs");

        migration.Should().Contain("INSERT INTO \"LoanPrepayments\"");
        migration.Should().Contain("mp.\"UserId\" = e.\"UserId\"");
        migration.Should().Contain("l.\"UserId\" = e.\"UserId\"");
        migration.Should().Contain("l.\"Name\" = left(e.\"Name\", length(e.\"Name\") - length(' - nadpłata'))");
        migration.Should().Contain("(l.\"TagId\" IS NULL AND e.\"TagId\" IS NULL)");
        migration.Should().Contain("OR l.\"TagId\" = e.\"TagId\"");
        migration.Should().Contain("e.\"LoanInstallmentId\" IS NULL");
        migration.Should().Contain("e.\"RegularExpenseDefinitionId\" IS NULL");
        migration.Should().Contain("e.\"ActualAmount\" > 0");
        migration.Should().Contain("e.\"Name\" LIKE '% - nadpłata'");
        migration.Should().Contain("e.\"IsDeleted\" = FALSE");
        migration.Should().Contain("COUNT(*) AS \"MatchCount\"");
        migration.Should().Contain("WHERE matched.\"MatchCount\" = 1");
    }

    [Fact]
    public void AddLoanPrepayments_Backfill_Should_Insert_Only_Representative_Unambiguous_Rows()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Execute(connection, """
            CREATE TABLE "Loans" (
                "Id" INTEGER NOT NULL,
                "UserId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "TagId" INTEGER NULL
            );
            CREATE TABLE "MonthPlans" (
                "Id" INTEGER NOT NULL,
                "UserId" TEXT NOT NULL,
                "Year" INTEGER NOT NULL,
                "Month" INTEGER NOT NULL
            );
            CREATE TABLE "Expenses" (
                "Id" INTEGER NOT NULL,
                "UserId" TEXT NOT NULL,
                "MonthPlanId" INTEGER NOT NULL,
                "Name" TEXT NOT NULL,
                "TagId" INTEGER NULL,
                "LoanInstallmentId" INTEGER NULL,
                "RegularExpenseDefinitionId" INTEGER NULL,
                "ActualAmount" NUMERIC NOT NULL,
                "IsDeleted" INTEGER NOT NULL
            );
            CREATE TABLE "LoanPrepayments" (
                "LoanId" INTEGER NOT NULL,
                "PrepaymentDate" TEXT NOT NULL,
                "Amount" NUMERIC NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            """);

        Execute(connection, """
            INSERT INTO "Loans" ("Id", "UserId", "Name", "TagId") VALUES
                (1, 'owner', 'A', NULL),
                (2, 'owner', 'B', 10),
                (3, 'owner', 'Duplicate', NULL),
                (4, 'owner', 'Duplicate', NULL),
                (5, 'other', 'A', NULL);
            INSERT INTO "MonthPlans" ("Id", "UserId", "Year", "Month") VALUES
                (1, 'owner', 2026, 7),
                (2, 'other', 2026, 7);
            INSERT INTO "Expenses" ("Id", "UserId", "MonthPlanId", "Name", "TagId", "LoanInstallmentId", "RegularExpenseDefinitionId", "ActualAmount", "IsDeleted") VALUES
                (1, 'owner', 1, 'A - nadpĹ‚ata', NULL, NULL, NULL, 100, 0),
                (2, 'owner', 1, 'A - nadpĹ‚ata', NULL, NULL, NULL, 50, 0),
                (3, 'owner', 1, 'B - nadpĹ‚ata', 10, NULL, NULL, 200, 0),
                (4, 'owner', 1, 'Duplicate - nadpĹ‚ata', NULL, NULL, NULL, 300, 0),
                (5, 'owner', 1, 'A - nadpĹ‚ata', 99, NULL, NULL, 400, 0),
                (6, 'owner', 1, 'A - nadpĹ‚ata', NULL, NULL, NULL, 0, 0),
                (7, 'owner', 1, 'A - nadpĹ‚ata', NULL, NULL, NULL, 700, 1),
                (8, 'other', 2, 'A - nadpĹ‚ata', NULL, NULL, NULL, 800, 0);
            """);

        Execute(connection, """
            INSERT INTO "LoanPrepayments" ("LoanId", "PrepaymentDate", "Amount", "CreatedAtUtc", "UpdatedAtUtc")
            SELECT
                matched."LoanId",
                printf('%04d-%02d-01', matched."Year", matched."Month"),
                matched."ActualAmount",
                datetime('now'),
                datetime('now')
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
                INNER JOIN "Loans" l
                    ON l."UserId" = e."UserId"
                    AND l."Name" = substr(e."Name", 1, length(e."Name") - length(' - nadpĹ‚ata'))
                    AND (
                        (l."TagId" IS NULL AND e."TagId" IS NULL)
                        OR l."TagId" = e."TagId"
                    )
                WHERE e."LoanInstallmentId" IS NULL
                    AND e."RegularExpenseDefinitionId" IS NULL
                    AND e."ActualAmount" > 0
                    AND e."Name" LIKE '% - nadpĹ‚ata'
                    AND e."IsDeleted" = 0
                GROUP BY e."Id", mp."Year", mp."Month", e."ActualAmount"
            ) matched
            WHERE matched."MatchCount" = 1;
            """);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "LoanId", "PrepaymentDate", "Amount"
            FROM "LoanPrepayments"
            ORDER BY "LoanId", "Amount";
            """;

        using var reader = command.ExecuteReader();
        var rows = new List<(long LoanId, string Date, decimal Amount)>();
        while (reader.Read())
        {
            rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetDecimal(2)));
        }

        rows.Should().Equal(
            (1, "2026-07-01", 50m),
            (1, "2026-07-01", 100m),
            (2, "2026-07-01", 200m),
            (5, "2026-07-01", 800m));
    }

    private static void Execute(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file '{relativePath}'.", relativePath);
    }
}
