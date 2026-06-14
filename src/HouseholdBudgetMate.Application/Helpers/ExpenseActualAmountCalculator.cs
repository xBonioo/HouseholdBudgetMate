using HouseholdBudgetMate.Domain.Entities;

namespace HouseholdBudgetMate.Application.Helpers;

internal static class ExpenseActualAmountCalculator
{
    public static decimal GetEffectiveActualAmount(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);

        return expense.LineItems.Count > 0
            ? expense.LineItems.Sum(x => x.Amount)
            : expense.ActualAmount;
    }

    public static decimal GetEffectiveActualTotal(IEnumerable<Expense> expenses)
    {
        ArgumentNullException.ThrowIfNull(expenses);

        return expenses.Sum(GetEffectiveActualAmount);
    }
}
