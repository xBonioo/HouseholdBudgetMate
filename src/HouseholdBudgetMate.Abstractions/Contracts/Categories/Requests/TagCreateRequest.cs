namespace HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;

public class TagCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public bool? SupportsLineItemsOverride { get; set; }
}