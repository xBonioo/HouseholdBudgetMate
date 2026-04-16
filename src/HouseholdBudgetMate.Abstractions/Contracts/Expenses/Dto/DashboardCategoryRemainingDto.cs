namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class DashboardCategoryRemainingDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal RemainingAmount { get; set; }
}