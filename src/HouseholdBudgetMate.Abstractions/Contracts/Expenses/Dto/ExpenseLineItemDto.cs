namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class ExpenseLineItemDto
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly OccurredAt { get; set; }
    public int? TagId { get; set; }
    public string? TagName { get; set; }
}