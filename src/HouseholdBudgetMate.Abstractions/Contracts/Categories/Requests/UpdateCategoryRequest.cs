namespace HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;

public class UpdateCategoryRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public bool SupportsLineItems { get; set; }
}