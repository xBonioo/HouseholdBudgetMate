using FluentAssertions;
using HouseholdBudgetMate.Migrations.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Reflection;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class LoanPrepaymentMigrationTests
{
    [Fact]
    public void AddLoanPrepayments_Should_Build_PostgreSql_Migration_Operations()
    {
        var migrationBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var migration = new AddLoanPrepayments();

        typeof(AddLoanPrepayments)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [migrationBuilder]);

        var createTable = migrationBuilder.Operations
            .OfType<CreateTableOperation>()
            .Single(x => x.Name == "LoanPrepayments");
        var sql = migrationBuilder.Operations
            .OfType<SqlOperation>()
            .Single()
            .Sql;

        createTable.Columns.Select(x => x.Name).Should().Contain(["LoanId", "PrepaymentDate", "Amount"]);
        sql.Should().Contain("make_date(matched.\"Year\", matched.\"Month\", 1)");
        sql.Should().Contain("NOW()");
        sql.Should().Contain("left(e.\"Name\", length(e.\"Name\") - length(' - nadpłata'))");
        sql.Should().Contain("c.\"Name\" = 'Kredyt'");
        sql.Should().NotContain("c.\"UserId\"");
        sql.Should().Contain("e.\"PlannedAmount\" = 0");
        sql.Should().Contain("e.\"ShowRemainingInUI\" = TRUE");
        sql.Should().Contain("WHERE matched.\"MatchCount\" = 1");
    }

    [Fact]
    public void AddLoanPrepayments_Should_Backfill_Only_Unambiguous_Legacy_Prepayment_Expenses()
    {
        var migration = ReadRepoFile(
            "src/HouseholdBudgetMate.Migrations/Migrations/20260623055248_AddLoanPrepayments.cs");

        migration.Should().Contain("INSERT INTO \"LoanPrepayments\"");
        migration.Should().Contain("mp.\"UserId\" = e.\"UserId\"");
        migration.Should().Contain("c.\"Name\" = 'Kredyt'");
        migration.Should().NotContain("c.\"UserId\"");
        migration.Should().Contain("l.\"UserId\" = e.\"UserId\"");
        migration.Should().Contain("l.\"Name\" = left(e.\"Name\", length(e.\"Name\") - length(' - nadpłata'))");
        migration.Should().Contain("(l.\"TagId\" IS NULL AND e.\"TagId\" IS NULL)");
        migration.Should().Contain("OR l.\"TagId\" = e.\"TagId\"");
        migration.Should().Contain("e.\"LoanInstallmentId\" IS NULL");
        migration.Should().Contain("e.\"RegularExpenseDefinitionId\" IS NULL");
        migration.Should().Contain("e.\"ActualAmount\" > 0");
        migration.Should().Contain("e.\"PlannedAmount\" = 0");
        migration.Should().Contain("e.\"ShowRemainingInUI\" = TRUE");
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
            CREATE TABLE "Categories" (
                "Id" INTEGER NOT NULL,
                "Name" TEXT NOT NULL
            );
            CREATE TABLE "Expenses" (
                "Id" INTEGER NOT NULL,
                "UserId" TEXT NOT NULL,
                "MonthPlanId" INTEGER NOT NULL,
                "Name" TEXT NOT NULL,
                "CategoryId" INTEGER NOT NULL,
                "TagId" INTEGER NULL,
                "LoanInstallmentId" INTEGER NULL,
                "RegularExpenseDefinitionId" INTEGER NULL,
                "PlannedAmount" NUMERIC NOT NULL,
                "ActualAmount" NUMERIC NOT NULL,
                "ShowRemainingInUI" INTEGER NOT NULL,
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
            INSERT INTO "Categories" ("Id", "Name") VALUES
                (1, 'Kredyt'),
                (2, 'Inne'),
                (3, 'Kredyt');
            INSERT INTO "Expenses" ("Id", "UserId", "MonthPlanId", "Name", "CategoryId", "TagId", "LoanInstallmentId", "RegularExpenseDefinitionId", "PlannedAmount", "ActualAmount", "ShowRemainingInUI", "IsDeleted") VALUES
                (1, 'owner', 1, 'A - nadpłata', 1, NULL, NULL, NULL, 0, 100, 1, 0),
                (2, 'owner', 1, 'A - nadpłata', 1, NULL, NULL, NULL, 0, 50, 1, 0),
                (3, 'owner', 1, 'B - nadpłata', 1, 10, NULL, NULL, 0, 200, 1, 0),
                (4, 'owner', 1, 'Duplicate - nadpłata', 1, NULL, NULL, NULL, 0, 300, 1, 0),
                (5, 'owner', 1, 'A - nadpłata', 1, 99, NULL, NULL, 0, 400, 1, 0),
                (6, 'owner', 1, 'A - nadpłata', 1, NULL, NULL, NULL, 0, 0, 1, 0),
                (7, 'owner', 1, 'A - nadpłata', 1, NULL, NULL, NULL, 0, 700, 1, 1),
                (8, 'other', 2, 'A - nadpłata', 3, NULL, NULL, NULL, 0, 800, 1, 0),
                (9, 'owner', 1, 'A - nadpłata', 2, NULL, NULL, NULL, 0, 900, 1, 0),
                (10, 'owner', 1, 'A - nadpłata', 1, NULL, NULL, NULL, 25, 1000, 1, 0),
                (11, 'owner', 1, 'A - nadpłata', 1, NULL, NULL, NULL, 0, 1100, 0, 0);
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
                INNER JOIN "Categories" c
                    ON c."Id" = e."CategoryId"
                    AND c."Name" = 'Kredyt'
                INNER JOIN "Loans" l
                    ON l."UserId" = e."UserId"
                    AND l."Name" = substr(e."Name", 1, length(e."Name") - length(' - nadpłata'))
                    AND (
                        (l."TagId" IS NULL AND e."TagId" IS NULL)
                        OR l."TagId" = e."TagId"
                    )
                WHERE e."LoanInstallmentId" IS NULL
                    AND e."RegularExpenseDefinitionId" IS NULL
                    AND e."ActualAmount" > 0
                    AND e."PlannedAmount" = 0
                    AND e."ShowRemainingInUI" = 1
                    AND e."Name" LIKE '% - nadpłata'
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
