using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using MudBlazor;

namespace HouseholdBudgetMate.Web.Components.Pages.PlanPage;

public partial class PlanPage
{
    private void ToggleLineItems(int expenseId)
    {
        if (!_expandedExpenseIds.Add(expenseId))
        {
            _expandedExpenseIds.Remove(expenseId);
            if (_editLineItemExpenseId == expenseId)
            {
                CancelLineItemEdit();
            }

            MarkDirtyStatePristine();
        }
    }

    private LineItemCreateDto GetLineItemCreateModel(int expenseId)
    {
        if (_lineItemCreateModels.TryGetValue(expenseId, out var model))
        {
            return model;
        }

        model = new LineItemCreateDto();
        _lineItemCreateModels[expenseId] = model;
        _lineItemCreateAmountInputs[expenseId] = FormatDecimalInput(model.Amount);
        return model;
    }

    private async Task CreateLineItemAsync(int expenseId)
    {
        if (!EnsureMonthEditable())
        {
            return;
        }

        var model = GetLineItemCreateModel(expenseId);
        if (string.IsNullOrWhiteSpace(model.Description))
        {
            Snackbar.Add("Podaj opis pozycji.", Severity.Warning);
            return;
        }

        try
        {
            if (!TryParseAmountOrWarn(GetLineItemCreateAmountInput(expenseId), out var lineItemAmount))
            {
                return;
            }

            model.Amount = lineItemAmount;

            await ExpenseService.CreateExpenseLineItemAsync(new CreateExpenseLineItemRequest
            {
                ExpenseId = expenseId,
                Description = model.Description,
                Amount = model.Amount,
                OccurredAt = model.OccurredAt,
                TagId = model.TagId
            }, CancellationToken.None);

            _lineItemCreateModels[expenseId] = new LineItemCreateDto();
            _lineItemCreateAmountInputs[expenseId] = FormatDecimalInput(0);
            await LoadAsync();
            _expandedExpenseIds.Add(expenseId);
            Snackbar.Add("Dodano pozycję.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private void BeginLineItemEdit(ExpenseLineItemDto lineItem, int expenseId)
    {
        _editLineItem = new UpdateExpenseLineItemRequest
        {
            Id = lineItem.Id,
            Description = lineItem.Description,
            Amount = lineItem.Amount,
            OccurredAt = lineItem.OccurredAt,
            TagId = lineItem.TagId
        };
        _editLineItemDate = lineItem.OccurredAt;
        _editLineItemExpenseId = expenseId;
        _editLineItemAmountInput = FormatDecimalInput(lineItem.Amount);
        MarkDirtyStatePristine();
    }

    private async Task SaveLineItemEditAsync()
    {
        if (!EnsureMonthEditable() || _editLineItem is null)
        {
            return;
        }

        try
        {
            if (!TryParseAmountOrWarn(_editLineItemAmountInput, out var lineItemAmount))
            {
                return;
            }

            _editLineItem.Amount = lineItemAmount;
            _editLineItem.OccurredAt = _editLineItemDate;
            await ExpenseService.UpdateExpenseLineItemAsync(_editLineItem, CancellationToken.None);

            var expandedExpenseId = _editLineItemExpenseId;
            CancelLineItemEdit();
            await LoadAsync();
            if (expandedExpenseId.HasValue)
            {
                _expandedExpenseIds.Add(expandedExpenseId.Value);
            }

            Snackbar.Add("Zapisano pozycję.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private void CancelLineItemEdit()
    {
        _editLineItem = null;
        _editLineItemAmountInput = FormatDecimalInput(0);
        _editLineItemDate = DateOnly.FromDateTime(DateTime.Today);
        _editLineItemExpenseId = null;
        MarkDirtyStatePristine();
    }

    private async Task DeleteLineItemAsync(int lineItemId, int expenseId)
    {
        if (!EnsureMonthEditable())
        {
            return;
        }

        try
        {
            await ExpenseService.DeleteExpenseLineItemAsync(new DeleteExpenseLineItemRequest { Id = lineItemId },
                CancellationToken.None);
            await LoadAsync();
            if (expenseId > 0)
            {
                _expandedExpenseIds.Add(expenseId);
            }

            Snackbar.Add("Usunięto pozycję.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }
}

