using FluentAssertions;

namespace HouseholdBudgetMate.Tests.Tests.Ui;

public sealed class LoanOperationRevertUiTests
{
    [Fact]
    public void AuditPage_Should_Render_Revert_Actions_Only_For_Supported_Loan_Operations()
    {
        var markup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Audit.razor");

        markup.Should().Contain("Operacje kredytowe");
        markup.Should().Contain("LoanOperationAuditTypes.LoanPrepayment");
        markup.Should().Contain("LoanOperationAuditTypes.LoanRateEntry");
        markup.Should().Contain("IsSupportedLoanOperation(operation)");
        markup.Should().Contain("Cofnij");
        markup.Should().Contain("Disabled=\"@(!operation.CanRevert)\"");
    }

    [Fact]
    public void AuditPage_Should_Confirm_Revert_Call_LoanService_And_Refresh()
    {
        var markup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Audit.razor");

        markup.Should().Contain("ConfirmRevertAsync");
        markup.Should().Contain("ConfirmDialog.Message");
        markup.Should().Contain("LoanService.RevertLoanOperationAsync");
        markup.Should().Contain("LoanOperationAuditId = operation.Id");
        markup.Should().Contain("ExpectedScheduleVersion = operation.ScheduleVersionAfter");
        markup.Should().Contain("await SearchCoreAsync()");
    }

    [Fact]
    public void AuditPage_Should_Show_Block_Reasons_And_Limit_Entity_Audit_To_Admins()
    {
        var markup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Audit.razor");

        markup.Should().Contain("RevertBlockedReason");
        markup.Should().Contain("GetLoanOperationStatusLabel");
        markup.Should().Contain("if (!HasBudgetSession)");
        markup.Should().Contain("if (IsAdminSession)");
        markup.Should().Contain("SearchLoanOperationsAsync");
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
