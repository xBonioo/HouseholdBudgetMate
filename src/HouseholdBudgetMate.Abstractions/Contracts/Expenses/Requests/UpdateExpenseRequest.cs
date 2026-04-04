namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class UpdateExpenseRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int CategoryId { get; set; }
    public int? TagId { get; set; }
    public decimal PlannedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public bool ShowRemainingInUI { get; set; }
}