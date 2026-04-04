using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Contracts.Accounts.Requests;

public class CreateAccountRequest
{
    public string Name { get; set; } = null!;
    public AccountType Type { get; set; }
    public decimal OpeningBalance { get; set; }
}

