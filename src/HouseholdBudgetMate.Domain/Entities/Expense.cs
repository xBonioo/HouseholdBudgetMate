using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class Expense : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public string UserId { get; set; } = User.DefaultUserId;
    public int MonthPlanId { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = null!;
    public int CategoryId { get; set; }
    public int? TagId { get; set; }
    public int? RegularExpenseDefinitionId { get; set; }
    public int? LoanInstallmentId { get; set; }
    public decimal PlannedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public bool ShowRemainingInUI { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public MonthPlan MonthPlan { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Tag? Tag { get; set; }
    public RegularExpenseDefinition? RegularExpenseDefinition { get; set; }
    public LoanInstallment? LoanInstallment { get; set; }
    public ICollection<ExpenseLineItem> LineItems { get; set; } = new List<ExpenseLineItem>();
}
