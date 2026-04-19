namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class CategoryYearStatisticsDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
    public decimal AverageMonthlySpent { get; set; }
    public int MonthsWithExpenses { get; set; }
}