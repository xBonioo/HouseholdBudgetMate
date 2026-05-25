using HouseholdBudgetMate.Web.Components.Dialogs;
using Microsoft.JSInterop;
using MudBlazor;

namespace HouseholdBudgetMate.Web.Components.Pages.PlanPage;

public partial class PlanPage : IAsyncDisposable
{
    protected override async Task OnParametersSetAsync()
    {
        if (Year < 2000)
        {
            Year = DateTime.Today.Year;
        }

        if (Month is < 1 or > 12)
        {
            Month = DateTime.Today.Month;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;

        try
        {
            _categories = (await CategoryService.GetAllAsync(CancellationToken.None)).ToList();
            _tagUsageCountByTagId = (await ExpenseService.GetTagUsageCountsAsync(CancellationToken.None))
                .ToDictionary(x => x.TagId, x => x.UsageCount);
            _accounts = (await AccountService.GetAllAsync(CancellationToken.None)).Where(x => !x.IsArchived).ToList();
            _monthPlan = await ExpenseService.GetMonthAsync(Year, Month, CancellationToken.None);
            _dashboardSummary = await ExpenseService.GetDashboardSummaryAsync(Year, Month, CancellationToken.None);

            _incomes = (await IncomeService.GetMonthIncomesAsync(Year, Month, CancellationToken.None)).ToList();
            _liveBalance = await IncomeService.GetLiveBalanceAsync(Year, Month, CancellationToken.None);

            _expandedExpenseIds.RemoveWhere(id => _monthPlan.Expenses.All(x => x.Id != id));
            _selectedExpenseIdsForCopy.RemoveWhere(id => _monthPlan.Expenses.All(x => x.Id != id));

            ApplyKpiFromMonthPlan();
            EnsureDefaultSelections();
            SyncMonthScopedDateDefaults();

            if (EditExpenseId.HasValue)
            {
                var expenseToEdit = _monthPlan.Expenses.FirstOrDefault(x => x.Id == EditExpenseId.Value);
                if (expenseToEdit is not null && _editExpense?.Id != expenseToEdit.Id)
                {
                    await StartEditAsync(expenseToEdit);
                }

                // Apply query-driven edit mode only once to avoid reopening edit after every reload.
                NavigationManager.NavigateTo($"/plan/{Year}/{Month}", replace: true);
            }
            else if (AddExpense && !_isCreateExpenseFormVisible)
            {
                _isCreateExpenseFormVisible = true;
                NavigationManager.NavigateTo($"/plan/{Year}/{Month}#create-expense-anchor", replace: true);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _incomeToggleViewportRef = DotNetObjectReference.Create(this);
            try
            {
                await JsRuntime.InvokeVoidAsync("startIncomeToggleViewportWatcher", _incomeToggleViewportRef, 1200);
            }
            catch (JSException)
            {
                // Ignore startup JS timing issues; the page still works without the watcher.
            }
        }

        if (!_expenseIdPendingScrollIntoView.HasValue)
        {
            return;
        }

        var expenseId = _expenseIdPendingScrollIntoView.Value;
        _expenseIdPendingScrollIntoView = null;
        try
        {
            await JsRuntime.InvokeVoidAsync("scrollExpenseRowIntoView", GetExpenseEditAnchorId(expenseId));
        }
        catch (JSException)
        {
            // Ignore startup JS timing issues; scroll is optional.
        }
    }

    [JSInvokable]
    public Task SetIncomePanelToggleVisibilityAsync(bool isVisible)
    {
        if (_isIncomePanelToggleVisible == isVisible && _isDesktopIncomePanelMode == isVisible)
        {
            return Task.CompletedTask;
        }

        _isDesktopIncomePanelMode = isVisible;
        _isIncomePanelToggleVisible = isVisible;

        // Resizing across the desktop/mobile breakpoint should reset the panel
        // so we never carry horizontal overlay state into mobile accordion mode.
        _isIncomePanelExpanded = false;
        _incomePanelExpandedWidthPx = 0;

        return InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("stopIncomeToggleViewportWatcher");
        }
        catch
        {
            // Ignore disposal-time JS disconnects.
        }

        _incomeToggleViewportRef?.Dispose();
        _incomeToggleViewportRef = null;
    }

    private void NavigateToMonth(int monthOffset, bool useCurrent = false)
    {
        var targetMonth = useCurrent
            ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            : new DateTime(Year, Month, 1).AddMonths(monthOffset);

        _editExpense = null;
        _isCopyMode = false;
        _selectedExpenseIdsForCopy.Clear();
        NavigationManager.NavigateTo($"/plan/{targetMonth.Year}/{targetMonth.Month}");
    }

    private void NavigateToRegularIncomes()
    {
        NavigationManager.NavigateTo("/recurring");
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        var parameters = new DialogParameters
        {
            [nameof(ConfirmDialog.Message)] = message
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<ConfirmDialog>(
            "Potwierdzenie",
            parameters,
            options);
        var result = await dialog.Result;

        return result is { Canceled: false };
    }

    private async Task ToggleMonthStatusAsync()
    {
        try
        {
            if (IsMonthClosed)
            {
                await ExpenseService.OpenMonthAsync(Year, Month, CancellationToken.None);
                Snackbar.Add("Miesiąc został otwarty.", Severity.Success);
            }
            else
            {
                var confirmation = await ConfirmAsync("Zamknąć miesiąc?");
                if (!confirmation)
                {
                    return;
                }

                await ExpenseService.CloseMonthAsync(Year, Month, CancellationToken.None);
                Snackbar.Add("Miesiąc został zamknięty.", Severity.Success);
            }

            await RefreshArchiveMonthsCacheAsync();

            await LoadAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task RefreshArchiveMonthsCacheAsync()
    {
        ArchiveCache.Invalidate();

        try
        {
            var months = await ExpenseService.GetAvailableMonthsAsync(CancellationToken.None);
            ArchiveCache.UpdateCache(months);
        }
        catch
        {
            // If refresh fails, leave cache invalidated so layout reloads on next access.
        }
    }

    private bool EnsureMonthEditable()
    {
        if (!IsMonthClosed)
        {
            return true;
        }

        Snackbar.Add("Miesiąc jest zamknięty. Edycja jest zablokowana.", Severity.Warning);
        return false;
    }

    private void EnsureDefaultSelections()
    {
        if (_newExpense.CategoryId == 0 && _categories.Count > 0)
        {
            _newExpense.CategoryId = _categories[0].Id;
        }

        if (_newIncome.AccountId == 0 && _accounts.Count > 0)
        {
            _newIncome.AccountId = _accounts[0].Id;
        }
    }

    private void SyncMonthScopedDateDefaults()
    {
        _newSavingsTransfer.Year = Year;
        _newSavingsTransfer.Month = Month;

        if (_newSavingsTransfer.TransferDate == default)
        {
            _newSavingsTransfer.TransferDate = new DateOnly(Year, Month, 1);
        }

        if (_editIncomeDate == default)
        {
            _editIncomeDate = new DateOnly(Year, Month, 1);
        }

        if (_editSavingsTransferDate == default)
        {
            _editSavingsTransferDate = new DateOnly(Year, Month, 1);
        }
    }
}

