namespace HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto;

public sealed class RegularIncomeDefinitionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal Amount { get; set; }
    public int DayOfMonth { get; set; }
    public int AccountId { get; set; }
    public string AccountName { get; set; } = null!;
    public bool IsActive { get; set; }
}