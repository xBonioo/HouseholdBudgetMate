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
        var expenses = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs");
        var page = string.Concat(markup, code, lifecycle);

        markup.Should().Contain(RemainingInPlanLabel);
        markup.Should().Contain("Live balance");
        markup.Should().Contain("Zaoszcz\u0119dzone w miesi\u0105cu");
        markup.Should().Contain("Brak danych");
        markup.Should().Contain(PreviousClosingBalanceRequired);
        markup.Should().Contain("ZAMKNIJ MIESI\u0104C");
        markup.Should().Contain("OTW\u00d3RZ MIESI\u0104C");
        markup.Should().Contain("Propozycje wydatk\u00f3w na bazie historii");
        markup.Should().Contain("Cykliczne wydatki nadal pojawi\u0105 si\u0119 automatycznie");
        markup.Should().Contain("KOPIUJ DO WYBRANEGO MIESI\u0104CA");
        markup.Should().Contain("Pomi\u0144 propozycje");
        markup.Should().Contain("Utw\u00f3rz miesi\u0105c z wybranych");
        markup.Should().Contain("Filtry wydatk\u00f3w");
        markup.Should().Contain("Kategoria");
        markup.Should().Contain("Status");
        markup.Should().Contain("Pozosta\u0142o do zap\u0142aty");
        markup.Should().Contain("Sp\u0142acone");
        markup.Should().Contain("Items=\"FilteredExpenses\"");
        markup.Should().Contain("Brak wydatk\u00f3w pasuj\u0105cych do filtr\u00f3w");
        markup.Should().Contain("Disabled=\"@(HasActiveExpenseFilters || IsFirstExpense(expense.Id))\"");
        markup.Should().Contain("Disabled=\"@(HasActiveExpenseFilters || IsLastExpense(expense.Id))\"");
        markup.Should().Contain("OnClick=\"SaveSavingsTransferEditAsync\"");
        markup.Should().NotContain("savings-transfer-editor-cell");
        markup.Should().NotContain("ActionForm OnSubmit=\"SaveSavingsTransferEditAsync\"");
        markup.Should().NotContain("Recurring expense will be auto-synced when the month is created.");
        markup.Should().NotContain("Loan installment will be auto-synced when the month is created.");

        code.Should().Contain("IsSelected = false");
        code.Should().NotContain("IsSelected = suggestion.IsAvailable");

        lifecycle.Should().Contain("ExpenseService.GetMonthPlanPreparationAsync");
        lifecycle.Should().Contain("ExpenseService.GetMonthAsync");
        lifecycle.Should().Contain("ExpenseService.GetDashboardSummaryAsync");
        lifecycle.Should().Contain("IncomeService.GetLiveBalanceAsync");
        lifecycle.Should().Contain("ExpenseService.CloseMonthAsync");
        lifecycle.Should().Contain("ExpenseService.OpenMonthAsync");
        lifecycle.Should().Contain("bypassPreparation");

        expenses.Should().Contain("ApplyMonthPlanSuggestionsAsync");
        expenses.Should().Contain("SkipMonthPlanSuggestionsAsync");
        expenses.Should().Contain("CopySelectedExpensesToMonthAsync");
        expenses.Should().Contain("SetMonthPlanSuggestionAmountInputAsync");
        expenses.Should().Contain("SetMonthPlanSuggestionSelectionAsync");
        expenses.Should().Contain("IsCopyTargetSameAsSource");
        expenses.Should().Contain("MatchesExpenseCategoryFilter");
        expenses.Should().Contain("MatchesExpensePaymentFilter");
        expenses.Should().Contain("ExpensePaymentFilter.RemainingToPay");
        expenses.Should().Contain("ExpensePaymentFilter.PaidOff");
        expenses.Should().Contain("expense.ActualAmount <= 0");
        expenses.Should().Contain("expense.ActualAmount > 0");
        expenses.Should().Contain("Wyczy\u015b\u0107 filtry, aby zmieni\u0107 kolejno\u015b\u0107 wydatk\u00f3w.");

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
        statisticsPage.Should().Contain("Plan roczny");
        statisticsPage.Should().Contain("Oczekiwane roczne wpływy");
        statisticsPage.Should().Contain("Oczekiwane roczne oszczędności");
        statisticsPage.Should().Contain("Zapisz plan roczny");
        statisticsPage.Should().Contain("Kandydaci alert");
        statisticsPage.Should().Contain("To są kandydaci do przyszłych powiadomień");
        statisticsPage.Should().Contain("Nic nie jest wysyłane automatycznie");
        statisticsPage.Should().Contain("Powyżej progu");
        statisticsPage.Should().Contain("Podsumowanie miesięczne (wpływy, plan, oszczędności)");
        statisticsPage.Should().Contain("Suma roczna");
        statisticsPage.Should().Contain("Średnia na miesiąc");
        statisticsPage.Should().Contain("GetYearSummaryTitle()");
        statisticsPage.Should().Contain("GetYearSummaryPeriod()");
        statisticsPage.Should().Contain("ExpenseService.UpsertAnnualPlanAsync");
        statisticsPage.Should().Contain("ExpenseService.GetYearStatisticsAsync");
        statisticsPage.Should().Contain("MonthlyFinance");
        statisticsPage.Should().Contain("PlannedAmount");
        statisticsPage.Should().Contain("SavingsTransferredAmount");
        statisticsPage.Should().Contain("AnnualPlan");
        statisticsPage.Should().Contain("DeviationAlertCandidates");
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
        var evidence = ReadRepoFile("context/changes/improve-monthly-planning/acceptance-evidence.md");

        evidence.Should().Contain("Automated Verification");
        evidence.Should().Contain("Service Evidence");
        evidence.Should().Contain("Manual Browser Smoke");
        evidence.Should().Contain("Pending manual");
        evidence.Should().Contain("GetMonthPlanPreparationAsync_Should_Not_Create_Target_Month");
        evidence.Should().Contain("CopySelectedExpensesToMonthAsync_Should_Skip_LoanBacked_Expenses");
        evidence.Should().Contain("UpsertAnnualPlanAsync_Should_Reject_Negative_Targets");
        evidence.Should().Contain("No `Safe-to-spend` / `SafeToSpend` output was reintroduced");
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
