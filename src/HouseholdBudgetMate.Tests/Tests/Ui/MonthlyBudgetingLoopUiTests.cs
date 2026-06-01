using FluentAssertions;

namespace HouseholdBudgetMate.Tests.Tests.Ui;

public sealed class MonthlyBudgetingLoopUiTests
{
    [Fact]
    public void PlanPage_Should_Surface_Accepted_Month_State_Without_SafeToSpend()
    {
        var markup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor");
        var code = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.cs");
        var lifecycle = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs");
        var page = string.Concat(markup, code, lifecycle);

        markup.Should().Contain("Pozostało w planie");
        markup.Should().Contain("Live balance");
        markup.Should().Contain("Zaoszczędzone w miesiącu");
        markup.Should().Contain("Brak danych");
        markup.Should().Contain("Wymagane saldo zamknięcia poprzedniego miesiąca");
        markup.Should().Contain("ZAMKNIJ MIESIĄC");
        markup.Should().Contain("OTWÓRZ MIESIĄC");

        lifecycle.Should().Contain("IncomeService.GetLiveBalanceAsync");
        lifecycle.Should().Contain("ExpenseService.CloseMonthAsync");
        lifecycle.Should().Contain("ExpenseService.OpenMonthAsync");

        page.Should().NotContain("Safe-to-spend");
        page.Should().NotContain("SafeToSpend");
    }

    [Fact]
    public void Dashboard_And_Accounts_Should_Use_Live_Balance_Contract_Without_SafeToSpend()
    {
        var dashboard = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Home.razor");
        var accountsMarkup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor");
        var accountsCode = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs");
        var surfaces = string.Concat(dashboard, accountsMarkup, accountsCode);

        dashboard.Should().Contain("Pozostało w planie");
        dashboard.Should().Contain("Live balance");
        dashboard.Should().Contain("Płynność miesiąca");
        dashboard.Should().Contain("IncomeService.GetLiveBalanceAsync");

        accountsMarkup.Should().Contain("Live balance");
        accountsCode.Should().Contain("IncomeService.GetLiveBalanceAsync");
        accountsCode.Should().Contain("HasCompleteBalanceBase");

        surfaces.Should().NotContain("Safe-to-spend");
        surfaces.Should().NotContain("SafeToSpend");
    }

    [Fact]
    public void Statistics_Should_Derive_Year_Summary_Range_From_Monthly_Finance_Data()
    {
        var statisticsPage = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor");

        statisticsPage.Should().Contain("GetYearSummaryTitle()");
        statisticsPage.Should().Contain("GetYearSummaryPeriod()");
        statisticsPage.Should().Contain("Math.Max(startMonth, today.Month)");
        statisticsPage.Should().NotContain("sty–{DateTimeProvider.GetLocalDateOnly()");
    }

    [Fact]
    public void Acceptance_Evidence_Should_Record_Controlled_Service_Scenario()
    {
        var evidence = ReadRepoFile("context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md");

        evidence.Should().Contain("Initial controlled state");
        evidence.Should().Contain("After real spend");
        evidence.Should().Contain("After due savings transfer");
        evidence.Should().Contain("After close/reopen/edit/close");
        evidence.Should().Contain("Closed-month edit blocking");
        evidence.Should().Contain("No separate safe-to-spend field");
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
