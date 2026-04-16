namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class CopySelectedExpensesToNextMonthRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public IReadOnlyList<int> ExpenseIds { get; set; } = [];
}