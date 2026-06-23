using FluentAssertions;

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
