using System.Globalization;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;
using MudBlazor;

namespace HouseholdBudgetMate.Web.Components.Pages.PlanPage;

public partial class PlanPage
{
    private Task ToggleIncomePanelMobileAsync()
    {
        _isIncomePanelExpanded = !_isIncomePanelExpanded;
        _incomePanelExpandedWidthPx = 0;
        return Task.CompletedTask;
    }

    private async Task ToggleIncomePanelExpandedAsync()
    {
        if (_isIncomePanelExpanded)
        {
            _isIncomePanelExpanded = false;
            _incomePanelExpandedWidthPx = 0;
            return;
        }

        _isIncomePanelExpanded = true;

        if (!_isDesktopIncomePanelMode)
        {
            _incomePanelExpandedWidthPx = 0;
            await InvokeAsync(StateHasChanged);
            return;
        }

        await InvokeAsync(StateHasChanged);

        var geometry = await JsRuntime.InvokeAsync<double[]>(
            "measureIncomePanelOverlayGeometry",
            new object?[] { _incomePanelWrapperRef });
        var measuredWidth = geometry.Length > 2 ? geometry[2] : 0;

        _incomePanelExpandedWidthPx = Math.Max(0, measuredWidth * 0.60d);
        await InvokeAsync(StateHasChanged);
    }

    private Task CollapseIncomePanelAsync()
    {
        _isIncomePanelExpanded = false;
        _incomePanelExpandedWidthPx = 0;
        return Task.CompletedTask;
    }

    private string GetIncomePanelClass()
    {
        return _isIncomePanelExpanded
            ? "pa-4 hb-panel income-panel income-panel-expanded"
            : "pa-4 hb-panel income-panel";
    }

    private string GetIncomePanelWrapperClass()
    {
        return _isIncomePanelExpanded
            ? "income-panel-wrapper income-panel-wrapper-expanded"
            : "income-panel-wrapper";
    }

    private string GetIncomePanelStyle()
    {
        if (!_isDesktopIncomePanelMode || !_isIncomePanelExpanded || _incomePanelExpandedWidthPx <= 0)
        {
            return string.Empty;
        }

        return $"width: {_incomePanelExpandedWidthPx.ToString("0.#", CultureInfo.InvariantCulture)}px;";
    }

    private string GetPlanDashboardGridClass()
    {
        return _isDesktopIncomePanelMode && _isIncomePanelExpanded
            ? "plan-dashboard-grid income-overlay-active"
            : "plan-dashboard-grid";
    }

    private async Task CreateIncomeAsync()
    {
        if (!EnsureMonthEditable())
        {
            return;
        }

        try
        {
            if (!TryParseAmountOrWarn(_newIncomeAmountInput, out var incomeAmount))
            {
                return;
            }

            _newIncome.Year = Year;
            _newIncome.Month = Month;
            _newIncome.IsRegular = false;
            _newIncome.Amount = incomeAmount;

            await IncomeService.CreateIncomeAsync(_newIncome, CancellationToken.None);

            _newIncome.Name = string.Empty;
            _newIncome.Amount = 0;
            _newIncomeAmountInput = FormatDecimalInput(0);
            _newIncome.ExpectedDayOfMonth = new DateOnly(Year, Month, 1);

            await LoadAsync();
            Snackbar.Add("Dodano wpływ.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private void StartIncomeEdit(IncomeDto income)
    {
        _editIncome = new UpdateIncomeRequest
        {
            Id = income.Id,
            Name = income.Name,
            Amount = income.Amount,
            AccountId = income.AccountId,
            ExpectedDayOfMonth = income.ExpectedDayOfMonth,
            IsRegular = false
        };

        _editIncomeDate = income.ExpectedDayOfMonth;
        _editIncomeAmountInput = FormatDecimalInput(income.Amount);
        MarkDirtyStatePristine();
    }

    private void CancelIncomeEdit()
    {
        _editIncome = null;
        _editIncomeDate = new DateOnly(Year, Month, 1);
        _editIncomeAmountInput = FormatDecimalInput(0);
        MarkDirtyStatePristine();
    }

    private async Task SaveIncomeEditAsync()
    {
        if (!EnsureMonthEditable() || _editIncome is null)
        {
            return;
        }

        try
        {
            if (!TryParseAmountOrWarn(_editIncomeAmountInput, out var incomeAmount))
            {
                return;
            }

            _editIncome.Amount = incomeAmount;
            _editIncome.ExpectedDayOfMonth = _editIncomeDate;
            await IncomeService.UpdateIncomeAsync(_editIncome, CancellationToken.None);

            _editIncome = null;
            _editIncomeAmountInput = FormatDecimalInput(0);
            await LoadAsync();
            Snackbar.Add("Zapisano wpływ.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task DeleteIncomeAsync(int incomeId)
    {
        if (!EnsureMonthEditable())
        {
            return;
        }

        var confirmation = await ConfirmAsync("Usunąć wpływ?");
        if (!confirmation)
        {
            return;
        }

        try
        {
            await IncomeService.DeleteIncomeAsync(new DeleteIncomeRequest { Id = incomeId }, CancellationToken.None);
            await LoadAsync();
            Snackbar.Add("Usunięto wpływ.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private void PrepareBonusFromIncome(IncomeDto income)
    {
        _newIncome.Name = $"{income.Name} - bonus";
        _newIncome.AccountId = income.AccountId;
        _newIncome.Amount = 0;
        _newIncomeAmountInput = FormatDecimalInput(0);
        _newIncome.ExpectedDayOfMonth = DateOnly.FromDateTime(DateTime.Today);
    }
}

