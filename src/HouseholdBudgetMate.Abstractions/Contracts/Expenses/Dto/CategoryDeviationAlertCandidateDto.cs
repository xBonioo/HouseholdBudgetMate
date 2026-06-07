namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class CategoryDeviationAlertCandidateDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal CurrentSpentAmount { get; set; }
    public decimal HistoricalAverageAmount { get; set; }
    public decimal DeviationPercent { get; set; }
    public decimal ThresholdPercent { get; set; } = 20m;
}
