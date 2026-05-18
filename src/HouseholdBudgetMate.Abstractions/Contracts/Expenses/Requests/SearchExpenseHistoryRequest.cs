namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class SearchExpenseHistoryRequest
{
    public string? Query { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int? CategoryId { get; set; }
    public int? RootTagId { get; set; }
    public int? SubTagId { get; set; }
    public int MaxResults { get; set; } = 200;
}
