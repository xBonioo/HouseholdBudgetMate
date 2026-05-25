using System.Globalization;
using MudBlazor;
using HouseholdBudgetMate.Abstractions.Parsing;

namespace HouseholdBudgetMate.Web.Components.Pages.PlanPage;

public partial class PlanPage
{
    private Task SetNewPlannedAmountInputAsync(string? input)
    {
        _newExpensePlannedAmountInput = input ?? string.Empty;
        if (LocalizedDecimalParser.TryParseOrZero(_newExpensePlannedAmountInput, out var value))
        {
            _newExpense.PlannedAmount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetNewActualAmountInputAsync(string? input)
    {
        _newExpenseActualAmountInput = input ?? string.Empty;
        if (LocalizedDecimalParser.TryParseOrZero(_newExpenseActualAmountInput, out var value))
        {
            _newExpense.ActualAmount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetEditPlannedAmountInputAsync(string? input)
    {
        _editExpensePlannedAmountInput = input ?? string.Empty;
        if (_editExpense is not null && LocalizedDecimalParser.TryParseOrZero(_editExpensePlannedAmountInput, out var value))
        {
            _editExpense.PlannedAmount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetEditActualAmountInputAsync(string? input)
    {
        _editExpenseActualAmountInput = input ?? string.Empty;
        if (_editExpense is not null && LocalizedDecimalParser.TryParseOrZero(_editExpenseActualAmountInput, out var value))
        {
            _editExpense.ActualAmount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetNewIncomeAmountInputAsync(string? input)
    {
        _newIncomeAmountInput = input ?? string.Empty;
        if (LocalizedDecimalParser.TryParseOrZero(_newIncomeAmountInput, out var value))
        {
            _newIncome.Amount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetEditIncomeAmountInputAsync(string? input)
    {
        _editIncomeAmountInput = input ?? string.Empty;
        if (_editIncome is not null && LocalizedDecimalParser.TryParseOrZero(_editIncomeAmountInput, out var value))
        {
            _editIncome.Amount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetNewSavingsTransferAmountInputAsync(string? input)
    {
        _newSavingsTransferAmountInput = input ?? string.Empty;
        if (LocalizedDecimalParser.TryParseOrZero(_newSavingsTransferAmountInput, out var value))
        {
            _newSavingsTransfer.Amount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetEditSavingsTransferAmountInputAsync(string? input)
    {
        _editSavingsTransferAmountInput = input ?? string.Empty;
        if (_editSavingsTransfer is not null &&
            LocalizedDecimalParser.TryParseOrZero(_editSavingsTransferAmountInput, out var value))
        {
            _editSavingsTransfer.Amount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetEditLineItemAmountInputAsync(string? input)
    {
        _editLineItemAmountInput = input ?? string.Empty;
        if (_editLineItem is not null && LocalizedDecimalParser.TryParseOrZero(_editLineItemAmountInput, out var value))
        {
            _editLineItem.Amount = value;
        }

        return Task.CompletedTask;
    }

    private string GetLineItemCreateAmountInput(int expenseId)
    {
        if (_lineItemCreateAmountInputs.TryGetValue(expenseId, out var input))
        {
            return input;
        }

        var model = GetLineItemCreateModel(expenseId);
        var formatted = FormatDecimalInput(model.Amount);
        _lineItemCreateAmountInputs[expenseId] = formatted;
        return formatted;
    }

    private Task SetLineItemCreateAmountInputAsync(int expenseId, string? input)
    {
        var normalizedInput = input ?? string.Empty;
        _lineItemCreateAmountInputs[expenseId] = normalizedInput;

        if (LocalizedDecimalParser.TryParseOrZero(normalizedInput, out var value))
        {
            GetLineItemCreateModel(expenseId).Amount = value;
        }

        return Task.CompletedTask;
    }

    private bool TryParseAmountOrWarn(string? input, out decimal value)
    {
        if (LocalizedDecimalParser.TryParseOrZero(input, out value))
        {
            return true;
        }

        Snackbar.Add("Niepoprawny format kwoty. Użyj np. 12,50 lub 12.50.", Severity.Warning);
        return false;
    }

    private static string FormatDecimalInput(decimal value)
    {
        return value.ToString("0.00", Culture);
    }

    private void UpdateDateIfProvided(DateTime? date, Action<DateOnly> apply, bool ensureCurrentMonth = true)
    {
        if (!date.HasValue)
        {
            return;
        }

        var dateOnly = DateOnly.FromDateTime(date.Value);

        if (ensureCurrentMonth && (dateOnly.Year != Year || dateOnly.Month != Month))
        {
            return;
        }

        apply(dateOnly);
    }

    private static bool IsExpenseNameTruncated(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Length > 23;
    }

    private static string TruncateExpenseName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= 23)
        {
            return name ?? string.Empty;
        }

        return $"{name[..23]}...";
    }
}

