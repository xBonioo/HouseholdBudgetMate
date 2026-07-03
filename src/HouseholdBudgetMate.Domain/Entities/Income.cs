using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class Income : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public string UserId { get; set; } = User.DefaultUserId;
    public int MonthPlanId { get; set; }
    public string Name { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly ExpectedDayOfMonth { get; set; }
    public int AccountId { get; set; }
    public bool IsRegular { get; set; }
    public int? RegularIncomeDefinitionId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public MonthPlan MonthPlan { get; set; } = null!;
    public Account Account { get; set; } = null!;
    public RegularIncomeDefinition? RegularIncomeDefinition { get; set; }
}
