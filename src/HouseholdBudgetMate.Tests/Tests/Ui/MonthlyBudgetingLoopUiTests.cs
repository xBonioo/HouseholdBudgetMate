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
        markup.Should().Contain("Disabled=\"@IsMonthClosed\"");
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
        lifecycle.Should().Contain("ExpenseService.GetMonthlyFinancialPictureAsync");
        lifecycle.Should().Contain("ExpenseService.GetDashboardSummaryAsync");
        lifecycle.Should().Contain("ExpenseService.CloseMonthAsync");
        lifecycle.Should().Contain("ExpenseService.OpenMonthAsync");
        lifecycle.Should().Contain("ExecutePostSaveAsync");
        lifecycle.Should().Contain("RefreshAfterSaveAsync");
        lifecycle.Should().Contain("PostSaveRefreshMode.FullReload");
        lifecycle.Should().Contain("PostSaveRefreshMode.BypassPreparation");
        lifecycle.Should().Contain("PostSaveRefreshMode.NoCurrentMonthReload");
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
        expenses.Should().Contain("ExecutePostSaveAsync");
        expenses.Should().Contain("PostSaveRefreshMode.FullReload");
        expenses.Should().Contain("PostSaveRefreshMode.BypassPreparation");
        expenses.Should().Contain("PostSaveRefreshMode.NoCurrentMonthReload");
        expenses.Should().Contain("RefreshArchiveMonthsCacheAsync");
        expenses.Should().NotContain("LoadAsync()");
        expenses.Should().NotContain("LoadAsync(bypassPreparation: true)");
        expenses.Should().Contain("Wyczy\u015b\u0107 filtry, aby zmieni\u0107 kolejno\u015b\u0107 wydatk\u00f3w.");

        page.Should().Contain(PreviousClosingBalanceGuidance);
        page.Should().Contain(StoredZeroBalanceGuidance);
        page.Should().Contain("MissingBalanceAccountNames");
        AssertNoSafeToSpend(page);
    }

    [Fact]
    public void PlanPage_Should_Preserve_Save_Handler_Wiring_And_Refresh_Modes()
    {
        var lifecycle = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs");
        var expenses = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs");
        var incomes = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Incomes.cs");
        var savingsTransfers = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.SavingsTransfers.cs");
        var lineItems = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs");

        lifecycle.Should().Contain("LoadAsync(bool bypassPreparation = false)");
        lifecycle.Should().Contain("ExpenseService.CloseMonthAsync");
        lifecycle.Should().Contain("ExpenseService.OpenMonthAsync");
        lifecycle.Should().Contain("RefreshArchiveMonthsCacheAsync");
        lifecycle.Should().Contain("bypassPreparation");

        expenses.Should().Contain("CreateExpenseAsync");
        expenses.Should().Contain("SaveEditAsync");
        expenses.Should().Contain("DeleteExpenseAsync");
        expenses.Should().Contain("MoveExpenseAsync");
        expenses.Should().Contain("CopySelectedExpensesAsync");
        expenses.Should().Contain("ApplyMonthPlanSuggestionsAsync");
        expenses.Should().Contain("SkipMonthPlanSuggestionsAsync");
        expenses.Should().Contain("ResetCopyTargetToNextMonth");
        expenses.Should().Contain("ClearMonthPreparation");
        expenses.Should().Contain("ConfirmAsync");

        incomes.Should().Contain("CreateIncomeAsync");
        incomes.Should().Contain("SaveIncomeEditAsync");
        incomes.Should().Contain("DeleteIncomeAsync");
        incomes.Should().Contain("ExecutePostSaveAsync");
        incomes.Should().Contain("PostSaveRefreshMode.FullReload");

        savingsTransfers.Should().Contain("CreateSavingsTransferAsync");
        savingsTransfers.Should().Contain("SaveSavingsTransferEditAsync");
        savingsTransfers.Should().Contain("DeleteSavingsTransferAsync");
        savingsTransfers.Should().Contain("ExecutePostSaveAsync");
        savingsTransfers.Should().Contain("PostSaveRefreshMode.FullReload");

        lineItems.Should().Contain("CreateLineItemAsync");
        lineItems.Should().Contain("SaveLineItemEditAsync");
        lineItems.Should().Contain("DeleteLineItemAsync");
        lineItems.Should().Contain("ExecutePostSaveAsync");
        lineItems.Should().Contain("PostSaveRefreshMode.FullReload");
        lineItems.Should().Contain("_expandedExpenseIds.Add(expenseId)");
    }

    [Fact]
    public void PlanPage_Should_Preserve_Expense_Special_Save_Flow_Contracts()
    {
        var expenses = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs");

        var copySelected = ExtractMethodBlock(expenses, "private async Task CopySelectedExpensesAsync()");
        copySelected.Should().Contain("ExpenseService.CopySelectedExpensesToMonthAsync");
        copySelected.Should().Contain("PostSaveRefreshMode.NoCurrentMonthReload");
        copySelected.Should().NotContain("LoadAsync(");

        var applySuggestions = ExtractMethodBlock(expenses, "private async Task ApplyMonthPlanSuggestionsAsync()");
        applySuggestions.Should().Contain("ExpenseService.ApplyMonthPlanSuggestionsAsync");
        applySuggestions.Should().Contain("PostSaveRefreshMode.BypassPreparation");
        applySuggestions.Should().Contain("RefreshArchiveMonthsCacheAsync))");
        applySuggestions.Should().NotContain("afterRefreshAsync: RefreshArchiveMonthsCacheAsync");
        AssertOccursBefore(applySuggestions, "ClearMonthPreparation();", "RefreshArchiveMonthsCacheAsync))");

        var skipSuggestions = ExtractMethodBlock(expenses, "private async Task SkipMonthPlanSuggestionsAsync()");
        skipSuggestions.Should().Contain("PostSaveRefreshMode.BypassPreparation");
        skipSuggestions.Should().Contain("afterRefreshAsync: RefreshArchiveMonthsCacheAsync");
        AssertOccursBefore(skipSuggestions, "ClearMonthPreparation();", "afterRefreshAsync: RefreshArchiveMonthsCacheAsync");
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

        accountsCode.Should().Contain("ExpenseService.GetMonthlyFinancialPictureAsync");
        accountsCode.Should().Contain("HasCompleteBalanceBase");
        accountsCode.Should().Contain("MissingBalanceAccountNames");
        accountsCode.Should().Contain(PreviousClosingBalanceGuidance);
        accountsCode.Should().Contain(StoredZeroBalanceGuidance);

        var balanceApplicability = ExtractMethodBlock(
            accountsCode,
            "private bool IsApplicableForSelectedMonthBalance(AccountDto account)");
        balanceApplicability.Should().Contain("if (!account.IsArchived)");
        AssertOccursBefore(balanceApplicability, "if (!account.IsArchived)", "account.ActiveFromUtc");

        AssertNoSafeToSpend(surface);
    }

    [Fact]
    public void Statistics_Should_Surface_Annual_And_Monthly_Finance_Contract_Without_LiveBalance()
    {
        var statisticsPage = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor");

        statisticsPage.Should().Contain("Statystyki roczne");
        statisticsPage.Should().Contain("Statystyki wydatków per kategoria");
        statisticsPage.Should().Contain("Suma wybranych kategorii");
        statisticsPage.Should().Contain("Średnia miesięczna wybranych kategorii");
        statisticsPage.Should().Contain("GetCategoryRangeSelectedTotalSpent()");
        statisticsPage.Should().Contain("GetCategoryRangeSelectedAverageMonthlySpent()");
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
        var evidence = ReadRepoFile("context/archive/2026-06-03-improve-monthly-planning/acceptance-evidence.md");

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

    private static string ExtractMethodBlock(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"method signature '{signature}' should exist");

        var openBrace = source.IndexOf('{', start);
        openBrace.Should().BeGreaterThanOrEqualTo(start, $"method signature '{signature}' should have a body");

        var depth = 0;
        for (var i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(start, i - start + 1);
                }
            }
        }

        throw new InvalidOperationException($"Could not extract method body for '{signature}'.");
    }

    private static void AssertOccursBefore(string source, string earlier, string later)
    {
        var earlierIndex = source.IndexOf(earlier, StringComparison.Ordinal);
        var laterIndex = source.IndexOf(later, StringComparison.Ordinal);

        earlierIndex.Should().BeGreaterThanOrEqualTo(0, $"'{earlier}' should exist");
        laterIndex.Should().BeGreaterThanOrEqualTo(0, $"'{later}' should exist");
        earlierIndex.Should().BeLessThan(laterIndex, $"'{earlier}' should occur before '{later}'");
    }

    private static void AssertNoSafeToSpend(string source)
    {
        source.Should().NotContain("Safe-to-spend");
        source.Should().NotContain("SafeToSpend");
    }
}
