namespace HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;

public sealed class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public bool SupportsLineItems { get; set; }
    public IReadOnlyList<TagDto> Tags { get; set; } = [];
}