namespace HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;

public class UpdateIncomeRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly ExpectedDayOfMonth { get; set; }
    public int AccountId { get; set; }
    public bool IsRegular { get; set; }
}