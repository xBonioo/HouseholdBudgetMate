using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class Account : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public string UserId { get; set; } = User.DefaultUserId;
    public int Order { get; set; }
    public string Name { get; set; } = null!;
    public int Type { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }

    public ICollection<AccountMonthBalance> MonthBalances { get; set; } = new List<AccountMonthBalance>();
}
