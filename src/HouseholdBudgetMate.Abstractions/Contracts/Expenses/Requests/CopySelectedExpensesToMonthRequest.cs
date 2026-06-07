namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class CopySelectedExpensesToMonthRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int TargetYear { get; set; }
    public int TargetMonth { get; set; }
    public IReadOnlyList<int> ExpenseIds { get; set; } = [];
}
