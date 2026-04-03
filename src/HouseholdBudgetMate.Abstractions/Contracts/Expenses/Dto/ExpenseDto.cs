namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class ExpenseDto
{
    public int Id { get; set; }
    public int MonthPlanId { get; set; }
    public string Name { get; set; } = null!;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int? TagId { get; set; }
    public string? TagName { get; set; }
    public decimal? PlannedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public bool ShowRemainingInUI { get; set; }
    public bool IsUnplanned => PlannedAmount is null or <= 0;
    public decimal RemainingAmount => (PlannedAmount ?? 0) - (ActualAmount ?? 0);
}

