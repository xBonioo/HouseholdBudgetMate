using FluentAssertions;

namespace HouseholdBudgetMate.Tests.Tests.Ui;

public sealed class MonthlyBudgetingLoopUiTests
{
    private const string RemainingInPlanLabel = "Pozosta\u0142o w planie";
    private const string PreviousClosingBalanceRequired = "Wymagane saldo zamkni\u0119cia poprzedniego miesi\u0105ca";
    private const string PreviousClosingBalanceGuidance = "salda zamkni\u0119cia kont za poprzedni miesi\u0105c";
    private const string StoredZeroBalanceGuidance = "Zapisana warto\u015b\u0107 0,00 PLN jest poprawnym saldem";

    [Fact]
    public void PlanPage_Should_Surface_Accepted_Month_State_Without_SafeToSpend()
    {
        var markup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor");
        var code = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.cs");
        var lifecycle = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs");
        var page = string.Concat(markup, code, lifecycle);

        markup.Should().Contain(RemainingInPlanLabel);
        markup.Should().Contain("Live balance");
        markup.Should().Contain("Zaoszcz\u0119dzone w miesi\u0105cu");
        markup.Should().Contain("Brak danych");
        markup.Should().Contain(PreviousClosingBalanceRequired);
        markup.Should().Contain("ZAMKNIJ MIESI\u0104C");
        markup.Should().Contain("OTW\u00d3RZ MIESI\u0104C");

        lifecycle.Should().Contain("ExpenseService.GetMonthAsync");
        lifecycle.Should().Contain("ExpenseService.GetDashboardSummaryAsync");
        lifecycle.Should().Contain("IncomeService.GetLiveBalanceAsync");
        lifecycle.Should().Contain("ExpenseService.CloseMonthAsync");
        lifecycle.Should().Contain("ExpenseService.OpenMonthAsync");

        page.Should().Contain(PreviousClosingBalanceGuidance);
        page.Should().Contain(StoredZeroBalanceGuidance);
        page.Should().Contain("MissingBalanceAccountNames");
        AssertNoSafeToSpend(page);
    }

    [Fact]
    public void Dashboard_Should_Surface_Monthly_Plan_And_Live_Balance_Contract_Without_SafeToSpend()
    {
        var dashboard = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Home.razor");

        dashboard.Should().Contain(RemainingInPlanLabel);
        dashboard.Should().Contain("Live balance");
        dashboard.Should().Contain("P\u0142ynno\u015b\u0107 miesi\u0105ca");
        dashboard.Should().Contain("Saldo poprzedniego miesi\u0105ca");
        dashboard.Should().Contain("AccountsBaseTotal");
        dashboard.Should().Contain("ExpenseService.GetDashboardSummaryAsync");
        dashboard.Should().Contain("IncomeService.GetLiveBalanceAsync");
        dashboard.Should().Contain(PreviousClosingBalanceGuidance);
        dashboard.Should().Contain(StoredZeroBalanceGuidance);
        dashboard.Should().Contain("MissingBalanceAccountNames");
        dashboard.Should().NotContain("CalculateCheckingAccountsBalance");
        AssertNoSafeToSpend(dashboard);
    }

    [Fact]
    public void Accounts_Should_Surface_Live_Balance_Account_Savings_And_Envelope_Contract()
    {
        var accountsMarkup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor");
        var accountsCode = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs");
        var surface = string.Concat(accountsMarkup, accountsCode);

        accountsMarkup.Should().Contain("Live balance");
        accountsMarkup.Should().Contain("Konta bie\u017c\u0105ce");
        accountsMarkup.Should().Contain("Konta + oszcz\u0119dno\u015bci");
        accountsMarkup.Should().Contain("Wp\u0142ywy / wydatki");
        accountsMarkup.Should().Contain("Pozosta\u0142o z limitu koperty");
        accountsMarkup.Should().Contain("Oszcz\u0119dno\u015bci i d\u0142ug");
        accountsMarkup.Should().Contain("Transfery oszcz\u0119dno\u015bciowe");
        accountsMarkup.Should().Contain("Salda kont");
        accountsMarkup.Should().NotContain(RemainingInPlanLabel);

        accountsCode.Should().Contain("IncomeService.GetLiveBalanceAsync");
        accountsCode.Should().Contain("ExpenseService.GetMonthAsync");
        accountsCode.Should().Contain("HasCompleteBalanceBase");
        accountsCode.Should().Contain("MissingBalanceAccountNames");
        accountsCode.Should().Contain(PreviousClosingBalanceGuidance);
        accountsCode.Should().Contain(StoredZeroBalanceGuidance);

        AssertNoSafeToSpend(surface);
    }

    [Fact]
    public void Statistics_Should_Surface_Annual_And_Monthly_Finance_Contract_Without_LiveBalance()
    {
        var statisticsPage = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor");

        statisticsPage.Should().Contain("Statystyki roczne");
        statisticsPage.Should().Contain("Podsumowanie miesi\u0119czne (wp\u0142ywy, plan, oszcz\u0119dno\u015bci)");
        statisticsPage.Should().Contain("Suma roczna");
        statisticsPage.Should().Contain("\u015arednia na miesi\u0105c");
        statisticsPage.Should().Contain("GetYearSummaryTitle()");
        statisticsPage.Should().Contain("GetYearSummaryPeriod()");
        statisticsPage.Should().Contain("ExpenseService.GetYearStatisticsAsync");
        statisticsPage.Should().Contain("MonthlyFinance");
        statisticsPage.Should().Contain("PlannedAmount");
        statisticsPage.Should().Contain("SavingsTransferredAmount");
        statisticsPage.Should().Contain("Math.Max(startMonth, today.Month)");
        statisticsPage.Should().NotContain("sty\u2013{DateTimeProvider.GetLocalDateOnly()");
        statisticsPage.Should().NotContain("Live balance");
        statisticsPage.Should().NotContain("GetLiveBalanceAsync");
        statisticsPage.Should().NotContain("HasCompleteBalanceBase");
        statisticsPage.Should().NotContain(PreviousClosingBalanceGuidance);
        AssertNoSafeToSpend(statisticsPage);
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

    private static void AssertNoSafeToSpend(string source)
    {
        source.Should().NotContain("Safe-to-spend");
        source.Should().NotContain("SafeToSpend");
    }
}
