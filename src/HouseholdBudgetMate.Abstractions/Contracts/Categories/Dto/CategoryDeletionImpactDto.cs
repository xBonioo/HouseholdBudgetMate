namespace HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;

public sealed class CategoryDeletionImpactDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int ExpenseCount { get; set; }
    public int ExpenseLineItemCount { get; set; }
    public bool HasAssignments => ExpenseCount > 0 || ExpenseLineItemCount > 0;
}
