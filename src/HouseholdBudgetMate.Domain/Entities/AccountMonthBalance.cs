using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class AccountMonthBalance : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal ClosingBalance { get; set; }

    public Account Account { get; set; } = null!;
}

