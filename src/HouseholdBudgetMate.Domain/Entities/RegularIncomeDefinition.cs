using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class RegularIncomeDefinition : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public string UserId { get; set; } = User.DefaultUserId;
    public string Name { get; set; } = null!;
    public decimal Amount { get; set; }
    public int DayOfMonth { get; set; }
    public int AccountId { get; set; }
    public bool IsActive { get; set; } = true;

    public Account Account { get; set; } = null!;
}
