using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Contracts.Accounts.Dto;

public sealed class AccountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public AccountType Type { get; set; }
    public int Order { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ActiveFromUtc { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public IReadOnlyList<AccountMonthBalanceDto> MonthBalances { get; set; } = [];
}
