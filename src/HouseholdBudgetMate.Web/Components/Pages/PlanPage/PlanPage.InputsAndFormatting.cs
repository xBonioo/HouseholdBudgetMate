using System.Globalization;
using MudBlazor;

namespace HouseholdBudgetMate.Web.Components.Pages.PlanPage;

public partial class PlanPage
{
    private Task SetNewPlannedAmountInputAsync(string? input)
    {
        _newExpensePlannedAmountInput = input ?? string.Empty;
        if (TryParseLocalizedDecimal(_newExpensePlannedAmountInput, out var value))
        {
            _newExpense.PlannedAmount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetNewActualAmountInputAsync(string? input)
    {
        _newExpenseActualAmountInput = input ?? string.Empty;
        if (TryParseLocalizedDecimal(_newExpenseActualAmountInput, out var value))
        {
            _newExpense.ActualAmount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetEditPlannedAmountInputAsync(string? input)
    {
        _editExpensePlannedAmountInput = input ?? string.Empty;
        if (_editExpense is not null && TryParseLocalizedDecimal(_editExpensePlannedAmountInput, out var value))
        {
            _editExpense.PlannedAmount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetEditActualAmountInputAsync(string? input)
    {
        _editExpenseActualAmountInput = input ?? string.Empty;
        if (_editExpense is not null && TryParseLocalizedDecimal(_editExpenseActualAmountInput, out var value))
        {
            _editExpense.ActualAmount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetNewIncomeAmountInputAsync(string? input)
    {
        _newIncomeAmountInput = input ?? string.Empty;
        if (TryParseLocalizedDecimal(_newIncomeAmountInput, out var value))
        {
            _newIncome.Amount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetEditIncomeAmountInputAsync(string? input)
    {
        _editIncomeAmountInput = input ?? string.Empty;
        if (_editIncome is not null && TryParseLocalizedDecimal(_editIncomeAmountInput, out var value))
        {
            _editIncome.Amount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetNewSavingsTransferAmountInputAsync(string? input)
    {
        _newSavingsTransferAmountInput = input ?? string.Empty;
        if (TryParseLocalizedDecimal(_newSavingsTransferAmountInput, out var value))
        {
            _newSavingsTransfer.Amount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetEditSavingsTransferAmountInputAsync(string? input)
    {
        _editSavingsTransferAmountInput = input ?? string.Empty;
        if (_editSavingsTransfer is not null &&
            TryParseLocalizedDecimal(_editSavingsTransferAmountInput, out var value))
        {
            _editSavingsTransfer.Amount = value;
        }

        return Task.CompletedTask;
    }

    private Task SetEditLineItemAmountInputAsync(string? input)
    {
        _editLineItemAmountInput = input ?? string.Empty;
        if (_editLineItem is not null && TryParseLocalizedDecimal(_editLineItemAmountInput, out var value))
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

        if (TryParseLocalizedDecimal(normalizedInput, out var value))
        {
            GetLineItemCreateModel(expenseId).Amount = value;
        }

        return Task.CompletedTask;
    }

    private bool TryParseAmountOrWarn(string? input, out decimal value)
    {
        if (TryParseLocalizedDecimal(input, out value))
        {
            return true;
        }

        Snackbar.Add("Niepoprawny format kwoty. Użyj np. 12,50 lub 12.50.", Severity.Warning);
        return false;
    }

    private static bool TryParseLocalizedDecimal(string? rawValue, out decimal value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        var normalized = rawValue
            .Trim()
            .Replace(" ", string.Empty)
            .Replace('\u00A0'.ToString(), string.Empty)
            .Replace(',', '.');

        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
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

