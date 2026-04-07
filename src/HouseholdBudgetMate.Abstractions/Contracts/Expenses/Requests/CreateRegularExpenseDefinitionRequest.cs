namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class CreateRegularExpenseDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int? TagId { get; set; }
    public decimal Amount { get; set; }
    public bool ShowRemainingInUI { get; set; } = true;
}