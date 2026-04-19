namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class YearStatisticsDto
{
    public int Year { get; set; }
    public IReadOnlyList<int> AvailableYears { get; set; } = [];
    public IReadOnlyList<int> PopulatedMonths { get; set; } = [];
    public IReadOnlyList<int> AccountBalanceMonths { get; set; } = [];
    public IReadOnlyList<CategoryYearStatisticsDto> CategoryStatistics { get; set; } = [];
    public IReadOnlyList<CategoryYearStatisticsDto> TopCategories { get; set; } = [];
    public IReadOnlyList<CategoryTagYearStatisticsDto> CategoryTagStatistics { get; set; } = [];
    public IReadOnlyList<YearCategoryBreakdownItemDto> CategoryBreakdown { get; set; } = [];
    public IReadOnlyList<YearMonthlyFinanceDto> MonthlyFinance { get; set; } = [];
    public IReadOnlyList<AccountYearBalanceDto> AccountBalances { get; set; } = [];
}