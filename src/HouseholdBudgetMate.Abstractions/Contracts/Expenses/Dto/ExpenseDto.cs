namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class ExpenseDto
{
    public int Id { get; set; }
    public int MonthPlanId { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = null!;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int? RegularExpenseDefinitionId { get; set; }
    public int? TagId { get; set; }
    public string? TagName { get; set; }
    public decimal PlannedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public bool SupportsLineItems { get; set; }
    public bool ShowRemainingInUI { get; set; }
    public IReadOnlyList<ExpenseLineItemDto> LineItems { get; set; } = [];
    public bool HasLineItems => LineItems.Count > 0;
    public bool IsUnplanned => PlannedAmount <= 0;
    public decimal RemainingAmount => PlannedAmount - ActualAmount;
}