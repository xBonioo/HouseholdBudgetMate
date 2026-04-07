using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class Category : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public decimal? EnvelopeLimit { get; set; }
    public bool SupportsLineItems { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAtUtc { get; set; }
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}