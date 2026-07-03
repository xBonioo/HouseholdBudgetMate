namespace HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;

public class DeleteCategoryRequest
{
    public int Id { get; set; }
    public int? ReplacementCategoryId { get; set; }
    public int? ReplacementTagId { get; set; }
}
