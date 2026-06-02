namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class AccountYearBalanceDto
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public IReadOnlyList<decimal?> MonthlyClosingBalances { get; set; } = [];
}
