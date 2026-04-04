namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class CreateExpenseRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Name { get; set; } = null!;
    public int CategoryId { get; set; }
    public int? TagId { get; set; }
    public decimal PlannedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public bool ShowRemainingInUI { get; set; } = true;
}