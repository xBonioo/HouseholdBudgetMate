namespace HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;

public sealed class TagDto
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public bool? SupportsLineItemsOverride { get; set; }
}