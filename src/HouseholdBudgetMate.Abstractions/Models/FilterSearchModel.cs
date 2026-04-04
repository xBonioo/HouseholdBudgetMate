namespace HouseholdBudgetMate.Abstractions.Models;

public class FilterSearchModel
{
    public string? Query { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}