using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class User : ATimestampable
{
    public const string DefaultUserId = "default-user";
    public const string TechnicalOwnerUsername = "__household_owner__";

    public string Id { get; set; } = DefaultUserId;
    public string Username { get; set; } = "default";
    public string PasswordHash { get; set; } = string.Empty;
    public int HouseholdMode { get; set; } = 1;
    public string BudgetOwnerUserId { get; set; } = DefaultUserId;
    public bool IsAdmin { get; set; }

    public User? BudgetOwnerUser { get; set; }
    public ICollection<User> SharedBudgetUsers { get; set; } = new List<User>();
}
