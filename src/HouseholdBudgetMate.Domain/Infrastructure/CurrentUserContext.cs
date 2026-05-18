using HouseholdBudgetMate.Domain.Entities;

namespace HouseholdBudgetMate.Domain.Infrastructure;

public sealed class CurrentUserContext
{
    public string UserId { get; set; } = User.DefaultUserId;
    public string? BudgetOwnerUserId { get; set; }
}
