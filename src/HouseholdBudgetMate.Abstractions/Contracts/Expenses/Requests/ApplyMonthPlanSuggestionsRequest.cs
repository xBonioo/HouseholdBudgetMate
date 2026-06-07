namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class ApplyMonthPlanSuggestionsRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public IReadOnlyList<ApplyMonthPlanSuggestionItemRequest> Suggestions { get; set; } = [];
}
