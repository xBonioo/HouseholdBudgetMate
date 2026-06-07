using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Web.Services;
using MudBlazor;

namespace HouseholdBudgetMate.Web.Components.Pages.PlanPage;

public partial class PlanPage
{
    private void ApplyKpiFromMonthPlan()
    {
        _kpi = _monthPlan?.Kpi ?? new MonthPlanKpiDto();
        RecomputePieChartData();
    }

    private Color GetRemainingBarColor()
    {
        return RemainingBudgetColorResolver.Resolve(_kpi.RemainingTotal, _kpi.RemainingPercent);
    }

    private string GetRowClass(ExpenseDto expense, int _)
    {
        if (expense.PlannedAmount <= 0)
        {
            return "hb-warning-row";
        }

        if (expense.ActualAmount > expense.PlannedAmount)
        {
            return "hb-danger-row";
        }

        return string.Empty;
    }

    private void SelectExpenseCategoryFilter(int? categoryId)
    {
        _selectedExpenseCategoryFilterId = _selectedExpenseCategoryFilterId == categoryId
            ? null
            : categoryId;
    }

    private void SelectExpensePaymentFilter(ExpensePaymentFilter filter)
    {
        _selectedExpensePaymentFilter = _selectedExpensePaymentFilter == filter
            ? ExpensePaymentFilter.All
            : filter;
    }

    private bool MatchesExpenseCategoryFilter(ExpenseDto expense)
    {
        return !_selectedExpenseCategoryFilterId.HasValue
               || expense.CategoryId == _selectedExpenseCategoryFilterId.Value;
    }

    private bool MatchesExpensePaymentFilter(ExpenseDto expense)
    {
        return _selectedExpensePaymentFilter switch
        {
            ExpensePaymentFilter.RemainingToPay => HasRemainingPayment(expense),
            ExpensePaymentFilter.PaidOff => IsPaidOff(expense),
            _ => true
        };
    }

    private static bool HasRemainingPayment(ExpenseDto expense)
    {
        return expense.PlannedAmount > 0
               && (expense.ActualAmount <= 0
                   || expense.ShowRemainingInUI && expense.RemainingAmount > 0);
    }

    private static bool IsPaidOff(ExpenseDto expense)
    {
        return expense.PlannedAmount > 0
               && expense.ActualAmount > 0
               && (!expense.ShowRemainingInUI || expense.RemainingAmount <= 0);
    }

    private static string FormatRemaining(bool showRemaining, decimal remaining)
    {
        return showRemaining ? $"{Math.Max(0, remaining).ToString("0.00", Culture)}zł" : "-";
    }

    private static string FormatPlannedAmount(decimal? plannedAmount)
    {
        return plannedAmount <= 0
            ? "-"
            : $"{plannedAmount!.Value.ToString("0.00", Culture)}zł";
    }

    private static string FormatRemainingForExpense(ExpenseDto expense)
    {
        if (expense.IsUnplanned || expense.PlannedAmount <= 0)
        {
            return "-";
        }

        return FormatRemaining(expense.ShowRemainingInUI, expense.RemainingAmount);
    }

    private async Task CreateExpenseAsync()
    {
        if (!EnsureMonthEditable())
        {
            return;
        }

        try
        {
            if (!TryParseNonNegativeAmountOrWarn(_newExpensePlannedAmountInput, out var plannedAmount)
                || !TryParseNonNegativeAmountOrWarn(_newExpenseActualAmountInput, out var actualAmount))
            {
                return;
            }

            _newExpense.PlannedAmount = plannedAmount;
            _newExpense.ActualAmount = SupportsLineItemsForSelection(_newExpense.CategoryId, _newExpense.TagId)
                ? 0
                : actualAmount;
            _newExpense.Year = Year;
            _newExpense.Month = Month;

            await ExpenseService.CreateExpenseAsync(_newExpense, CancellationToken.None);

            ResetCreateExpenseForm();
            await LoadAsync();
            Snackbar.Add("Dodano wydatek.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private void ToggleCreateExpenseForm()
    {
        if (!EnsureMonthEditable())
        {
            return;
        }

        _isCreateExpenseFormVisible = !_isCreateExpenseFormVisible;
        MarkDirtyStatePristine();
    }

    private Task StartEditAsync(ExpenseDto expense)
    {
        _editExpense = new UpdateExpenseRequest
        {
            Id = expense.Id,
            Name = expense.Name,
            CategoryId = expense.CategoryId,
            TagId = expense.TagId,
            PlannedAmount = expense.PlannedAmount,
            ActualAmount = expense.ActualAmount,
            ShowRemainingInUI = expense.ShowRemainingInUI,
        };

        _editExpenseRootTagId = GetRootTagId(expense.TagId);

        if (expense.HasLineItems)
        {
            _editExpense.ActualAmount = expense.LineItems.Sum(x => x.Amount);
        }

        if (SupportsLineItemsForSelection(_editExpense.CategoryId, _editExpenseRootTagId))
        {
            _editExpense.TagId = _editExpenseRootTagId;
        }

        _editExpensePlannedAmountInput = FormatDecimalInput(_editExpense.PlannedAmount);
        _editExpenseActualAmountInput = FormatDecimalInput(_editExpense.ActualAmount);

        _expenseIdPendingScrollIntoView = expense.Id;
        MarkDirtyStatePristine();
        return Task.CompletedTask;
    }

    private static string GetExpenseEditAnchorId(int expenseId) => $"expense-edit-anchor-{expenseId}";

    private void CancelEdit()
    {
        _editExpense = null;
        _editExpenseRootTagId = null;
        _editExpensePlannedAmountInput = FormatDecimalInput(0);
        _editExpenseActualAmountInput = FormatDecimalInput(0);
        MarkDirtyStatePristine();
    }

    private async Task SaveEditAsync()
    {
        if (!EnsureMonthEditable() || _editExpense is null)
        {
            return;
        }

        try
        {
            if (!TryParseNonNegativeAmountOrWarn(_editExpensePlannedAmountInput, out var plannedAmount)
                || !TryParseNonNegativeAmountOrWarn(_editExpenseActualAmountInput, out var actualAmount))
            {
                return;
            }

            _editExpense.PlannedAmount = plannedAmount;
            _editExpense.ActualAmount = SupportsLineItemsForSelection(_editExpense.CategoryId, _editExpense.TagId)
                ? 0
                : actualAmount;

            await ExpenseService.UpdateExpenseAsync(_editExpense, CancellationToken.None);
            CancelEdit();
            await LoadAsync();
            Snackbar.Add("Zapisano wydatek.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task DeleteExpenseAsync(int expenseId)
    {
        if (!EnsureMonthEditable())
        {
            return;
        }

        var confirmation = await ConfirmAsync("Usunąć wydatek?");
        if (!confirmation)
        {
            return;
        }

        try
        {
            await ExpenseService.DeleteExpenseAsync(new DeleteExpenseRequest { Id = expenseId },
                CancellationToken.None);
            await LoadAsync();
            Snackbar.Add("Usunięto wydatek.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task MoveExpenseAsync(int expenseId, int direction)
    {
        if (!EnsureMonthEditable())
        {
            return;
        }

        if (HasActiveExpenseFilters)
        {
            Snackbar.Add("Wyczyść filtry, aby zmienić kolejność wydatków.", Severity.Info);
            return;
        }

        var ordered = OrderedExpenses.ToList();
        var index = ordered.FindIndex(x => x.Id == expenseId);

        if (index == -1)
        {
            return;
        }

        var newIndex = index + direction;
        if (newIndex < 0 || newIndex >= ordered.Count)
        {
            return;
        }

        (ordered[index], ordered[newIndex]) = (ordered[newIndex], ordered[index]);

        try
        {
            await ExpenseService.ReorderExpensesAsync(new ReorderExpensesRequest
            {
                ExpenseIds = ordered.Select(x => x.Id).ToList()
            }, CancellationToken.None);

            await LoadAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private void ToggleCopyMode()
    {
        _isCopyMode = !_isCopyMode;
        if (!_isCopyMode)
        {
            _selectedExpenseIdsForCopy.Clear();
            return;
        }

        ResetCopyTargetToNextMonth();
    }

    private Task SetExpenseCopySelection(int expenseId, bool isSelected)
    {
        if (isSelected)
        {
            _selectedExpenseIdsForCopy.Add(expenseId);
        }
        else
        {
            _selectedExpenseIdsForCopy.Remove(expenseId);
        }

        return Task.CompletedTask;
    }

    private async Task CopySelectedExpensesAsync()
    {
        if (_selectedExpenseIdsForCopy.Count == 0)
        {
            Snackbar.Add("Wybierz co najmniej jedną pozycję do skopiowania.", Severity.Warning);
            return;
        }

        if (IsCopyTargetSameAsSource)
        {
            Snackbar.Add("Wybierz inny miesiąc docelowy niż bieżący.", Severity.Warning);
            return;
        }

        var targetMonthLabel = new DateTime(_copyTargetYear, _copyTargetMonth, 1).ToString("MMMM yyyy", Culture);
        var confirmation = await ConfirmAsync(
            $"Skopiować {_selectedExpenseIdsForCopy.Count} pozycji do {targetMonthLabel}?");
        if (!confirmation)
        {
            return;
        }

        try
        {
            var orderedSelectedExpenseIds = OrderedExpenses
                .Where(x => _selectedExpenseIdsForCopy.Contains(x.Id))
                .Select(x => x.Id)
                .ToList();

            var copiedCount = await ExpenseService.CopySelectedExpensesToMonthAsync(
                new CopySelectedExpensesToMonthRequest
                {
                    Year = Year,
                    Month = Month,
                    TargetYear = _copyTargetYear,
                    TargetMonth = _copyTargetMonth,
                    ExpenseIds = orderedSelectedExpenseIds
                },
                CancellationToken.None);

            _selectedExpenseIdsForCopy.Clear();
            _isCopyMode = false;

            if (copiedCount == 0)
            {
                Snackbar.Add("Nie skopiowano żadnej pozycji. Wybrane pozycje mogły zostać już uzupełnione automatycznie.", Severity.Info);
                return;
            }

            Snackbar.Add($"Skopiowano {copiedCount} pozycji do {targetMonthLabel}.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private Task SetMonthPlanSuggestionSelectionAsync(int sourceExpenseId, bool isSelected)
    {
        var draft = _monthPlanSuggestionDrafts.FirstOrDefault(x => x.Suggestion.SourceExpenseId == sourceExpenseId);
        if (draft is not null && draft.Suggestion.IsAvailable)
        {
            draft.IsSelected = isSelected;
        }

        return Task.CompletedTask;
    }

    private Task SetMonthPlanSuggestionAmountInputAsync(int sourceExpenseId, string? input)
    {
        var draft = _monthPlanSuggestionDrafts.FirstOrDefault(x => x.Suggestion.SourceExpenseId == sourceExpenseId);
        if (draft is not null)
        {
            draft.PlannedAmountInput = input ?? string.Empty;
        }

        return Task.CompletedTask;
    }

    private async Task ApplyMonthPlanSuggestionsAsync()
    {
        if (!HasMonthPreparation)
        {
            return;
        }

        var selectedDrafts = _monthPlanSuggestionDrafts
            .Where(x => x.IsSelected && x.Suggestion.IsAvailable)
            .OrderBy(x => x.Suggestion.SourceExpenseId)
            .ToList();

        if (selectedDrafts.Count == 0)
        {
            Snackbar.Add("Wybierz co najmniej jedną propozycję do zastosowania.", Severity.Warning);
            return;
        }

        var selectedRequests = new List<ApplyMonthPlanSuggestionItemRequest>(selectedDrafts.Count);
        foreach (var draft in selectedDrafts)
        {
            if (!TryParseAmountOrWarn(draft.PlannedAmountInput, out var plannedAmount))
            {
                return;
            }

            selectedRequests.Add(new ApplyMonthPlanSuggestionItemRequest
            {
                SourceExpenseId = draft.Suggestion.SourceExpenseId,
                PlannedAmount = plannedAmount
            });
        }

        try
        {
            var createdCount = await ExpenseService.ApplyMonthPlanSuggestionsAsync(
                new ApplyMonthPlanSuggestionsRequest
                {
                    Year = Year,
                    Month = Month,
                    Suggestions = selectedRequests
                },
                CancellationToken.None);

            _isCopyMode = false;
            _selectedExpenseIdsForCopy.Clear();
            ClearMonthPreparation();

            await RefreshArchiveMonthsCacheAsync();
            await LoadAsync(bypassPreparation: true);

            var totalSelected = selectedRequests.Count;
            if (createdCount == 0)
            {
                Snackbar.Add("Nie dodano żadnych propozycji. Cykliczne wydatki i tak zostały uzupełnione.", Severity.Info);
                return;
            }

            if (createdCount < totalSelected)
            {
                Snackbar.Add($"Dodano {createdCount} z {totalSelected} wybranych propozycji. Część pozycji została już dodana automatycznie.", Severity.Info);
                return;
            }

            Snackbar.Add($"Dodano {createdCount} propozycji historycznych.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task SkipMonthPlanSuggestionsAsync()
    {
        if (!HasMonthPreparation)
        {
            return;
        }

        try
        {
            _isCopyMode = false;
            _selectedExpenseIdsForCopy.Clear();
            ClearMonthPreparation();

            await LoadAsync(bypassPreparation: true);
            await RefreshArchiveMonthsCacheAsync();

            Snackbar.Add("Miesiąc został utworzony bez propozycji historycznych. Cykliczne wydatki dodano automatycznie.", Severity.Info);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private bool IsFirstExpense(int expenseId)
    {
        return OrderedExpenses.Count == 0 || OrderedExpenses[0].Id == expenseId;
    }

    private bool IsLastExpense(int expenseId)
    {
        return OrderedExpenses.Count == 0 || OrderedExpenses[^1].Id == expenseId;
    }

    private string GetMonthName(int month)
    {
        return new DateTime(2000, month, 1).ToString("MMMM", Culture);
    }

    private bool SupportsLineItemsForSelection(int categoryId, int? tagId)
    {
        if (tagId.HasValue)
        {
            var tagOverride = _categories
                .SelectMany(x => x.Tags)
                .FirstOrDefault(x => x.Id == tagId.Value)
                ?.SupportsLineItemsOverride;

            if (tagOverride.HasValue)
            {
                return tagOverride.Value;
            }
        }

        return _categories.FirstOrDefault(x => x.Id == categoryId)?.SupportsLineItems == true;
    }

    private IReadOnlyList<EnvelopeProgressItemDto> BuildEnvelopeProgressItems()
    {
        if (_monthPlan is null)
        {
            return [];
        }

        var spentByCategory = _monthPlan.Expenses
            .GroupBy(x => x.CategoryId)
            .ToDictionary(x => x.Key, x => x.Sum(e => e.ActualAmount));

        var plannedByCategory = _monthPlan.Expenses
            .GroupBy(x => x.CategoryId)
            .ToDictionary(x => x.Key, x => x.Sum(e => e.PlannedAmount));

        var categoriesWithEnvelope = _categories
            .Where(x => x.EnvelopeLimit is > 0)
            .OrderByDescending(x => x.EnvelopeLimit)
            .ThenBy(x => x.Name)
            .ToList();

        return categoriesWithEnvelope
            .Select(category =>
            {
                var limit = category.EnvelopeLimit!.Value;
                var spent = spentByCategory.GetValueOrDefault(category.Id, 0);
                var planned = plannedByCategory.GetValueOrDefault(category.Id, 0);
                var ratio = limit <= 0 ? 0 : (double)(spent / limit * 100);

                return new EnvelopeProgressItemDto
                {
                    CategoryName = category.Name,
                    SpentAmount = spent,
                    PlannedAmount = planned,
                    LimitAmount = limit,
                    ProgressPercent = Math.Clamp(ratio, 0, 100),
                    Color = GetEnvelopeColor(ratio)
                };
            })
            .ToList();
    }

    private string? BuildCreateExpenseEnvelopeWarning()
    {
        if (!_isCreateExpenseFormVisible || _monthPlan is null)
        {
            return null;
        }

        var selectedCategory = _categories.FirstOrDefault(x => x.Id == _newExpense.CategoryId);
        if (selectedCategory?.EnvelopeLimit is not > 0)
        {
            return null;
        }

        var currentSpent = _monthPlan.Expenses
            .Where(x => x.CategoryId == _newExpense.CategoryId)
            .Sum(x => x.ActualAmount);

        var predictedSpent = currentSpent + _newExpense.ActualAmount;
        var limit = selectedCategory.EnvelopeLimit.Value;

        if (predictedSpent <= limit)
        {
            return null;
        }

        return
            $"Uwaga: po dodaniu wydatku kategoria '{selectedCategory.Name}' przekroczy limit koperty ({predictedSpent.ToString("0.00", Culture)} / {limit.ToString("0.00", Culture)}zł).";
    }

    private static Color GetEnvelopeColor(double usagePercent)
    {
        if (usagePercent > 100)
        {
            return Color.Error;
        }

        if (usagePercent >= 75)
        {
            return Color.Warning;
        }

        return Color.Success;
    }

    private void ResetCreateExpenseForm()
    {
        _newExpense.Name = string.Empty;
        _newExpenseRootTagId = null;
        _newExpense.TagId = null;
        _newExpense.PlannedAmount = 0;
        _newExpense.ActualAmount = 0;
        _newExpensePlannedAmountInput = FormatDecimalInput(0);
        _newExpenseActualAmountInput = FormatDecimalInput(0);
        _newExpense.ShowRemainingInUI = false;
        _isCreateExpenseFormVisible = false;
    }
}
