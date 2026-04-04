using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Contracts.Accounts.Requests;

public class UpdateAccountRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public AccountType Type { get; set; }
}

