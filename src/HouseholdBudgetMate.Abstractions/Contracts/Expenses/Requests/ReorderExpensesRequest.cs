namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class ReorderExpensesRequest
{
    public IReadOnlyList<int> ExpenseIds { get; set; } = [];
}