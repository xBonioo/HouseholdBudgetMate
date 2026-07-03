namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class EnvelopeProgressItemDto
{
    public string CategoryName { get; init; } = string.Empty;
    public decimal SpentAmount { get; init; }
    public decimal PlannedAmount { get; init; }
    public decimal LimitAmount { get; init; }
    public double ProgressPercent { get; init; }
    public string ColorStatus { get; init; } = "Success";
}
