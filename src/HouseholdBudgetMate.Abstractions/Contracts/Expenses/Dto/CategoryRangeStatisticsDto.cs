namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class CategoryRangeStatisticsDto
{
    public IReadOnlyList<CategoryYearStatisticsDto> CategoryStatistics { get; set; } = [];
    public IReadOnlyList<CategoryTagYearStatisticsDto> CategoryTagStatistics { get; set; } = [];
    public int RangeMonthCount { get; set; }
    public int? FirstYear { get; set; }
    public int? LastYear { get; set; }
}
