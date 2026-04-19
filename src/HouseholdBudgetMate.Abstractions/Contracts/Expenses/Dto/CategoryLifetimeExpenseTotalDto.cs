namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class CategoryLifetimeExpenseTotalDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
    public int? FirstYear { get; set; }
    public int? LastYear { get; set; }
}