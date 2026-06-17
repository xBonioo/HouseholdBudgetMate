using FluentAssertions;

namespace HouseholdBudgetMate.Tests.Tests.Ui;

public sealed class LoanUiRedesignTests
{
    [Fact]
    public void LoanSelectionPrompt_Should_Explain_How_To_Start_Working_With_Loans()
    {
        var markup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanSelectionPrompt.razor");

        markup.Should().Contain("Wybierz kredyt");
        markup.Should().Contain("Zaznacz kredyt z listy po lewej albo dodaj nowy");
        markup.Should().Contain("Harmonogram, WIBOR, koszty i ustawienia pojawi");
    }

    [Fact]
    public void LoanWorkspaceTabs_Should_Render_The_Selected_Loan_Servicing_Areas()
    {
        var loansPageMarkup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor");
        var tabsMarkup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanWorkspaceTabs.razor");
        var summaryMarkup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanSummaryHeader.razor");
        var listMarkup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanListPanel.razor");

        loansPageMarkup.Should().Contain("_selectedLoanTab = LoanWorkspaceTab.Summary;");
        tabsMarkup.Should().Contain("Podsumowanie");
        tabsMarkup.Should().Contain("Harmonogram");
        tabsMarkup.Should().Contain("WIBOR");
        tabsMarkup.Should().Contain("Koszty");
        tabsMarkup.Should().Contain("Ustawienia");
        summaryMarkup.Should().NotContain("mt-4 hb-panel");
        tabsMarkup.Should().NotContain("mt-4 hb-panel");
        listMarkup.Should().Contain("Zar");
    }

    [Fact]
    public void LoanScheduleTable_Should_Surface_Filter_Toolbar_And_Bank_Update_Entry_Point()
    {
        var tableMarkup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanScheduleTable.razor");
        var toolbarMarkup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanScheduleToolbar.razor");
        var actionsMarkup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanScheduleRowActions.razor");

        tableMarkup.Should().Contain("Skr");
        tableMarkup.Should().Contain("Nast");
        tableMarkup.Should().Contain("LoanScheduleRowActions");
        toolbarMarkup.Should().Contain("Reset filtr");
        toolbarMarkup.Should().Contain("Wszystkie lata");
        toolbarMarkup.Should().Contain("Sp");
        toolbarMarkup.Should().Contain("Niesp");
        actionsMarkup.Should().Contain("Nadp");
        actionsMarkup.Should().Contain("Skr");
    }

    [Fact]
    public void LoanBankScheduleUpdateDialog_Should_Describe_The_Bank_Workflow()
    {
        var markup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanBankScheduleUpdateDialog.razor");

        markup.Should().Contain("Zmiana raty z banku");
        markup.Should().Contain("Zmienimy przysz");
        markup.Should().Contain("Kwota raty z banku");
        markup.Should().Contain("Data ostatniej raty");
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
