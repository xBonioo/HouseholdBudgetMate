namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class ExpenseHistorySearchResultDto
{
    public int ExpenseId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string ExpenseName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? RootTagId { get; set; }
    public string? RootTagName { get; set; }
    public int? SubTagId { get; set; }
    public string? SubTagName { get; set; }
    public decimal PlannedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public string? MatchingDescription { get; set; }
}