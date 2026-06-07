namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class MonthPlanExpenseSuggestionDto
{
    public int SourceExpenseId { get; set; }
    public string Name { get; set; } = null!;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int? TagId { get; set; }
    public string? TagName { get; set; }
    public decimal SourcePlannedAmount { get; set; }
    public decimal SourceActualAmount { get; set; }
    public decimal SuggestedPlannedAmount { get; set; }
    public string Reason { get; set; } = null!;
    public bool IsAvailable { get; set; } = true;
    public string? UnavailableReason { get; set; }
}
