namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class MonthPlanSuggestionDraftDto
{
    public MonthPlanSuggestionDraftDto(MonthPlanExpenseSuggestionDto suggestion, string plannedAmountInput)
    {
        Suggestion = suggestion;
        PlannedAmountInput = plannedAmountInput;
    }

    public MonthPlanExpenseSuggestionDto Suggestion { get; }
    public bool IsSelected { get; set; }
    public string PlannedAmountInput { get; set; }
}
