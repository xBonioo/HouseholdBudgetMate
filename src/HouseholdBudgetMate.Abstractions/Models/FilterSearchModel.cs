namespace HouseholdBudgetMate.Abstractions.Models;

public class FilterSearchModel
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Query { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}