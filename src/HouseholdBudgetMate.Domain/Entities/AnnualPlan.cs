using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class AnnualPlan : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public string UserId { get; set; } = User.DefaultUserId;
    public int Year { get; set; }
    public decimal ExpectedIncomeAmount { get; set; }
    public decimal ExpectedSavingsAmount { get; set; }
}
