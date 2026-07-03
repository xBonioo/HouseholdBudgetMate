namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed record AnnualPlanComparisonRowDto(string Label, decimal PlannedAmount, decimal ActualAmount)
{
    public decimal Difference => ActualAmount - PlannedAmount;
}
