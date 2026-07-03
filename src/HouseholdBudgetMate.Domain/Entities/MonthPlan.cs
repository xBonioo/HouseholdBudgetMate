using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class MonthPlan : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public string UserId { get; set; } = User.DefaultUserId;
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IsClosed { get; set; }

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<Income> Incomes { get; set; } = new List<Income>();
    public ICollection<MonthSavingsTransferItem> SavingsTransfers { get; set; } = new List<MonthSavingsTransferItem>();
}
