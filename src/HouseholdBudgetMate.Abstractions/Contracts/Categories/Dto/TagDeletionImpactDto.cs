namespace HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;

public sealed class TagDeletionImpactDto
{
    public int TagId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int ExpenseCount { get; set; }
    public int ExpenseLineItemCount { get; set; }
    public bool HasAssignments => ExpenseCount > 0 || ExpenseLineItemCount > 0;
}
