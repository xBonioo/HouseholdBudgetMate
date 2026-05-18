using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class MonthSavingsTransferItem : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public string UserId { get; set; } = User.DefaultUserId;
    public int MonthPlanId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly TransferDate { get; set; }

    public MonthPlan MonthPlan { get; set; } = null!;
}
