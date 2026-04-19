namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class CategoryTagYearStatisticsDto
{
    public int CategoryId { get; set; }
    public int? TagId { get; set; }
    public int? ParentTagId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public int Depth { get; set; }
    public bool HasChildren { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal AverageMonthlySpent { get; set; }
    public int MonthsWithExpenses { get; set; }
}