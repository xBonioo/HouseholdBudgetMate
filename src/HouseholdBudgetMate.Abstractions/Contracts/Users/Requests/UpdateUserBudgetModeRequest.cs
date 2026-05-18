using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Contracts.Users.Requests;

public class UpdateUserBudgetModeRequest
{
    public string UserId { get; set; } = string.Empty;
    public HouseholdMode HouseholdMode { get; set; } = HouseholdMode.SeparateBudget;
    public string? BudgetOwnerUserId { get; set; }
}
