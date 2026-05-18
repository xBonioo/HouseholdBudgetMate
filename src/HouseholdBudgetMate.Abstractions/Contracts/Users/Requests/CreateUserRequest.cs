using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Contracts.Users.Requests;

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
    public HouseholdMode HouseholdMode { get; set; } = HouseholdMode.SeparateBudget;
    public string? BudgetOwnerUserId { get; set; }
}
