namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class YearCategoryBreakdownItemDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public IReadOnlyList<decimal> MonthlySpent { get; set; } = [];
}