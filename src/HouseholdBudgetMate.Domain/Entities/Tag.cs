using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class Tag : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int CategoryId { get; set; }
    public int? ParentTagId { get; set; }
    public bool? SupportsLineItemsOverride { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public Category Category { get; set; } = null!;
    public Tag? ParentTag { get; set; }
    public ICollection<Tag> ChildTags { get; set; } = new List<Tag>();
}
