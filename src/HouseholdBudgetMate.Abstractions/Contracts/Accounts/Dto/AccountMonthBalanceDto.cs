namespace HouseholdBudgetMate.Abstractions.Contracts.Accounts.Dto;

public sealed class AccountMonthBalanceDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal ClosingBalance { get; set; }
}