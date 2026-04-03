namespace HouseholdBudgetMate.Domain.Infrastructure;

public abstract class ATimestampable : ITimestampable
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public interface ITimestampable
{
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}