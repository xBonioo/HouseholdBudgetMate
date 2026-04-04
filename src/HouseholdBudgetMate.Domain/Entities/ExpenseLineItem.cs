using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class ExpenseLineItem : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly OccurredAt { get; set; }
    public int? TagId { get; set; }

    public Expense Expense { get; set; } = null!;
    public Tag? Tag { get; set; }
}