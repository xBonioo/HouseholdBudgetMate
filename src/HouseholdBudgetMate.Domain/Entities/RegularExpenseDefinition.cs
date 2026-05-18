using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class RegularExpenseDefinition : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public string UserId { get; set; } = User.DefaultUserId;
    public int Order { get; set; }
    public string Name { get; set; } = null!;
    public int CategoryId { get; set; }
    public int? TagId { get; set; }
    public decimal Amount { get; set; }
    public bool IsActive { get; set; } = true;
    public bool ShowRemainingInUI { get; set; } = true;

    public Category Category { get; set; } = null!;
    public Tag? Tag { get; set; }
}
