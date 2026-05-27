using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Contracts.Users.Dto;

public sealed class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public HouseholdMode HouseholdMode { get; set; }
    public string BudgetOwnerUserId { get; set; } = string.Empty;
    public string? BudgetOwnerUsername { get; set; }
    public bool HasPin { get; set; }
    public bool IsInteractive { get; set; }
    public string SessionSecurityStamp { get; set; } = string.Empty;
    public bool IsDefaultAdmin { get; set; }
    public bool IsAdmin { get; set; }
}
