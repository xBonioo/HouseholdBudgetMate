namespace HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;

public class TagCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public int? ParentTagId { get; set; }
    public bool? SupportsLineItemsOverride { get; set; }
}