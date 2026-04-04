namespace HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto;

public sealed class IncomeDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Name { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly ExpectedDayOfMonth { get; set; }
    public int AccountId { get; set; }
    public string AccountName { get; set; } = null!;
    public bool IsRegular { get; set; }
}