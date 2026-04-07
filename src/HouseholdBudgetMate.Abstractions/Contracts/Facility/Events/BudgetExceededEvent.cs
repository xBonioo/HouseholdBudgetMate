namespace HouseholdBudgetMate.Abstractions.Contracts.Facility.Events;

public sealed class BudgetExceededEvent
{
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal EnvelopeLimit { get; init; }
    public decimal SpentAmount { get; init; }
}