namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class DashboardMonthlySavingsDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal PlannedAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal IncomeAmount { get; set; }
    public decimal SavedAmount { get; set; }
}