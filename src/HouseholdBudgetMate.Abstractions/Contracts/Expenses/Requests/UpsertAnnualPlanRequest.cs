namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class UpsertAnnualPlanRequest
{
    public int Year { get; set; }
    public decimal ExpectedIncomeAmount { get; set; }
    public decimal ExpectedSavingsAmount { get; set; }
}
