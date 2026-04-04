namespace HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;

public class UpdateRegularIncomeDefinitionRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal Amount { get; set; }
    public int DayOfMonth { get; set; }
    public int AccountId { get; set; }
    public bool IsActive { get; set; }
}