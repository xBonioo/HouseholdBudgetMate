namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class MonthPlanKpiDto
{
    public decimal PlannedTotal { get; set; }
    public decimal SpentTotal { get; set; }
    public decimal RemainingTotal { get; set; }
    public double RemainingPercent { get; set; }
}