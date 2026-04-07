namespace HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;

public class CreateCategoryRequest
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public decimal? EnvelopeLimit { get; set; }
    public bool SupportsLineItems { get; set; }
}