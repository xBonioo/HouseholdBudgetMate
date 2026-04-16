namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class DashboardSummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int TransactionCount { get; set; }
    public decimal UnplannedSpentTotal { get; set; }
    public decimal SavedAmountThisMonth { get; set; }
    public decimal SavedAmountYearToDate { get; set; }
    public decimal AverageMonthlyIncome { get; set; }
    public decimal AverageMonthlySpent { get; set; }
    public decimal AverageMonthlySaved { get; set; }
    public IReadOnlyList<DashboardCategoryRemainingDto> CategoryRemainingItems { get; set; } = [];
    public IReadOnlyList<DashboardMonthlySavingsDto> SavingsTimeline { get; set; } = [];
}