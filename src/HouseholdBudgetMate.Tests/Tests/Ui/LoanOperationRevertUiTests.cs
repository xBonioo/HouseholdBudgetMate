using FluentAssertions;

namespace HouseholdBudgetMate.Tests.Tests.Ui;

public sealed class LoanOperationRevertUiTests
{
    [Fact]
    public void AuditPage_Should_Not_Render_Loan_Operation_Revert_Actions_When_Loans_Are_Disabled()
    {
        var markup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Audit.razor");

        markup.Should().NotContain("Operacje kredytowe");
        markup.Should().NotContain("LoanOperationAuditTypes.LoanPrepayment");
        markup.Should().NotContain("LoanOperationAuditTypes.LoanRateEntry");
        markup.Should().NotContain("IsSupportedLoanOperation(operation)");
        markup.Should().NotContain("ConfirmRevertAsync");
        markup.Should().NotContain("LoanService.RevertLoanOperationAsync");
        markup.Should().NotContain("LoanOperationAuditId = operation.Id");
    }

    [Fact]
    public void AuditPage_Should_Limit_Entity_Audit_To_Admins_Without_Loan_Operation_Search()
    {
        var markup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Audit.razor");

        markup.Should().NotContain("RevertBlockedReason");
        markup.Should().NotContain("GetLoanOperationStatusLabel");
        markup.Should().Contain("if (!HasBudgetSession)");
        markup.Should().Contain("if (IsAdminSession)");
        markup.Should().NotContain("SearchLoanOperationsAsync");
        markup.Should().Contain("SearchAsync");
    }

    private static string ReadRepoFile(string relativePath)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(root, relativePath));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HouseholdBudgetMate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
