namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class UpdateRegularExpenseDefinitionRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int? TagId { get; set; }
    public decimal Amount { get; set; }
    public bool IsActive { get; set; }
    public bool ShowRemainingInUI { get; set; } = true;
}