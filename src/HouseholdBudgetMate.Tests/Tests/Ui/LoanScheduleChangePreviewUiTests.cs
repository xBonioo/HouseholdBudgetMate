using Bunit;
using FluentAssertions;
using HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Web.Components.Pages.LoansPage;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;

namespace HouseholdBudgetMate.Tests.Tests.Ui;

public sealed class LoanScheduleChangePreviewUiTests : BunitContext
{
    public LoanScheduleChangePreviewUiTests()
    {
        Services.AddMudServices();
        JSInterop.SetupVoid("mudKeyInterceptor.connect", _ => true);
    }

    [Fact]
    public void Dialog_Should_Render_Summary_And_Action_Labels()
    {
        var markup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanScheduleChangePreviewDialog.razor");

        markup.Should().Contain("Wróć do edycji");
        markup.Should().Contain("Potwierdź i zapisz");
        markup.Should().Contain("loan-schedule-change-preview-dialog__content");
        markup.Should().Contain("loan-schedule-change-preview-dialog__actions");
    }

    [Fact]
    public void YearPanel_Should_Expand_The_First_Affected_Year_And_Group_Rows()
    {
        var cut = Render<LoanSchedulePreviewYearPanel>(parameters => parameters
            .Add(component => component.Rows, BuildPreview().Rows)
            .Add(component => component.AffectedFrom, new DateOnly(2025, 4, 1)));

        cut.Markup.Should().Contain("2025");
        cut.Markup.Should().Contain("2026");
        cut.Markup.Should().Contain("04.2025");
        cut.Markup.Should().Contain("Po: 2\u00a0950,00 PLN");
        cut.Markup.Should().NotContain("Dodana");
        cut.Markup.Should().Contain("Usunięta");
    }

    [Fact]
    public void YearPanel_Should_Expand_First_Visible_Year_When_Affected_Year_Has_Only_Paid_Rows()
    {
        var rows = new[]
        {
            new LoanScheduleComparisonRowDto
            {
                DueDate = new DateOnly(2026, 12, 15),
                State = LoanScheduleComparisonRowState.Unchanged,
                BeforeIsPaid = true,
                AfterIsPaid = true,
                Before = new ScheduleRowDto(2026, 12, new DateOnly(2026, 12, 15), 3_000m, 2_100m, 900m),
                After = new ScheduleRowDto(2026, 12, new DateOnly(2026, 12, 15), 3_000m, 2_100m, 900m)
            },
            new LoanScheduleComparisonRowDto
            {
                DueDate = new DateOnly(2027, 1, 15),
                State = LoanScheduleComparisonRowState.Changed,
                Before = new ScheduleRowDto(2027, 1, new DateOnly(2027, 1, 15), 3_000m, 2_120m, 880m),
                After = new ScheduleRowDto(2027, 1, new DateOnly(2027, 1, 15), 2_950m, 2_110m, 840m)
            }
        };

        var cut = Render<LoanSchedulePreviewYearPanel>(parameters => parameters
            .Add(component => component.Rows, rows)
            .Add(component => component.AffectedFrom, new DateOnly(2026, 12, 15)));

        cut.Markup.Should().Contain("2027");
        cut.Markup.Should().Contain("01.2027");
        cut.Markup.Should().NotContain("12.2026");
    }

    [Fact]
    public void ComparisonTable_Should_Render_Before_And_After_Columns()
    {
        var cut = Render<LoanSchedulePreviewTable>(parameters => parameters
            .Add(component => component.Rows, BuildPreview().Rows)
            .Add(component => component.Culture, new("pl-PL")));

        cut.Markup.Should().Contain("Miesiąc");
        cut.Markup.Should().Contain("Przed rata");
        cut.Markup.Should().Contain("Po rata");
        cut.Markup.Should().Contain("Przed koszty");
        cut.Markup.Should().Contain("Po koszty");
        cut.Markup.Should().Contain("Rata zniknęła po przeliczeniu.");
    }

    [Fact]
    public void ComparisonTable_Should_Render_Project_Charges()
    {
        var rows = new[]
        {
            new LoanScheduleComparisonRowDto
            {
                DueDate = new DateOnly(2025, 4, 1),
                State = LoanScheduleComparisonRowState.Changed,
                Before = new ScheduleRowDto(2025, 4, new DateOnly(2025, 4, 1), 3_100m, 2_100m, 800m, 200m),
                After = new ScheduleRowDto(2025, 4, new DateOnly(2025, 4, 1), 2_950m, 2_110m, 760m, 80m)
            }
        };

        var cut = Render<LoanSchedulePreviewTable>(parameters => parameters
            .Add(component => component.Rows, rows)
            .Add(component => component.Culture, new("pl-PL")));

        cut.Markup.Should().Contain("200,00 PLN");
        cut.Markup.Should().Contain("80,00 PLN");
    }

    [Fact]
    public void Dialog_Should_Render_User_Facing_ChangeLabel_In_Title()
    {
        var markup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanScheduleChangePreviewDialog.razor");

        markup.Should().Contain("Preview.ChangeLabel}: {Preview.LoanName");
        markup.Should().NotContain("Preview.ChangeType}: {Preview.LoanName");
    }

    private static LoanScheduleChangePreviewDto BuildPreview()
    {
        return new LoanScheduleChangePreviewDto
        {
            LoanId = 7,
            LoanName = "Hipoteczny",
            ChangeType = "Nadpłata",
            ChangeLabel = "Podgląd nadpłaty",
            AffectedFrom = new DateOnly(2025, 4, 1),
            SourceVersion = "version-1",
            BeforeSummary = new LoanScheduleSummaryDto
            {
                RemainingPrincipal = 800_000m,
                NextInstallment = 3_000m,
                TotalFutureInterest = 210_000m,
                EndDate = new DateOnly(2035, 12, 31),
                InstallmentCount = 120
            },
            AfterSummary = new LoanScheduleSummaryDto
            {
                RemainingPrincipal = 799_900m,
                NextInstallment = 2_950m,
                TotalFutureInterest = 209_100m,
                EndDate = new DateOnly(2035, 10, 31),
                InstallmentCount = 118
            },
            Rows =
            [
                new LoanScheduleComparisonRowDto
                {
                    DueDate = new DateOnly(2025, 4, 1),
                    State = LoanScheduleComparisonRowState.Changed,
                    Before = new ScheduleRowDto(2025, 4, new DateOnly(2025, 4, 1), 3_000m, 2_100m, 800m),
                    After = new ScheduleRowDto(2025, 4, new DateOnly(2025, 4, 1), 2_950m, 2_110m, 760m)
                },
                new LoanScheduleComparisonRowDto
                {
                    DueDate = new DateOnly(2025, 5, 1),
                    State = LoanScheduleComparisonRowState.Removed,
                    Before = new ScheduleRowDto(2025, 5, new DateOnly(2025, 5, 1), 3_000m, 2_120m, 780m),
                    After = null
                },
                new LoanScheduleComparisonRowDto
                {
                    DueDate = new DateOnly(2026, 1, 1),
                    State = LoanScheduleComparisonRowState.Added,
                    Before = null,
                    After = new ScheduleRowDto(2026, 1, new DateOnly(2026, 1, 1), 2_900m, 2_130m, 770m)
                },
                new LoanScheduleComparisonRowDto
                {
                    DueDate = new DateOnly(2025, 6, 1),
                    State = LoanScheduleComparisonRowState.Unchanged,
                    BeforeIsPaid = true,
                    AfterIsPaid = true,
                    Before = new ScheduleRowDto(2025, 6, new DateOnly(2025, 6, 1), 3_000m, 2_110m, 790m),
                    After = new ScheduleRowDto(2025, 6, new DateOnly(2025, 6, 1), 3_000m, 2_110m, 790m)
                }
            ]
        };
    }

    [Fact]
    public void Paid_Rows_Should_Not_Be_Shown_In_The_Preview_Table()
    {
        var cut = Render<LoanSchedulePreviewTable>(parameters => parameters
            .Add(component => component.Rows, BuildPreview().Rows)
            .Add(component => component.Culture, new("pl-PL")));

        cut.Markup.Should().NotContain("06.2025");
        cut.Markup.Should().Contain("04.2025");
    }

    [Fact]
    public void YearPanel_Should_Refresh_Amounts_When_Recalculated_Rows_Have_The_Same_Date_Range()
    {
        var preview = BuildPreview();
        var cut = Render<LoanSchedulePreviewYearPanel>(parameters => parameters
            .Add(component => component.Rows, preview.Rows)
            .Add(component => component.AffectedFrom, preview.AffectedFrom)
            .Add(component => component.Culture, new("pl-PL")));

        cut.Markup.Should().Contain("Po: 2\u00a0900,00 PLN");

        var recalculatedRows = preview.Rows
            .Select(row => row.DueDate == new DateOnly(2025, 4, 1)
                ? new LoanScheduleComparisonRowDto
                {
                    DueDate = row.DueDate,
                    State = row.State,
                    Before = row.Before,
                    After = new ScheduleRowDto(2025, 4, row.DueDate, 2_800m, 2_000m, 700m)
                }
                : row)
            .ToList();

        cut.Render(parameters => parameters
            .Add(component => component.Rows, recalculatedRows)
            .Add(component => component.AffectedFrom, preview.AffectedFrom)
            .Add(component => component.Culture, new("pl-PL")));

        cut.Markup.Should().Contain("Po: 2\u00a0800,00 PLN");
        cut.Markup.Should().NotContain("Po: 2\u00a0950,00 PLN");
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
