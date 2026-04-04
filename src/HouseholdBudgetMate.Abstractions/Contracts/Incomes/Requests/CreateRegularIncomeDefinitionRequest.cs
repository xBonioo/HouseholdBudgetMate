namespace HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;

public class CreateRegularIncomeDefinitionRequest
{
    public string Name { get; set; } = null!;
    public decimal Amount { get; set; }
    public int DayOfMonth { get; set; }
    public int AccountId { get; set; }
}