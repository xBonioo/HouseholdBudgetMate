namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class LineItemCreateDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly OccurredAt { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int? TagId { get; set; }
}