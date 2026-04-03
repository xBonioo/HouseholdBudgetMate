namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class CreateExpenseLineItemRequest
{
    public int ExpenseId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly OccurredAt { get; set; }
    public int? TagId { get; set; }
}

