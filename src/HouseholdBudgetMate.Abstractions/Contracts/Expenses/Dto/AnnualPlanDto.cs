namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class AnnualPlanDto
{
    public int Year { get; set; }
    public decimal ExpectedIncomeAmount { get; set; }
    public decimal ExpectedSavingsAmount { get; set; }
}
