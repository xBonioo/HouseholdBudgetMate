namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class MonthPlanPreparationDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public bool MonthExists { get; set; }
    public int SourceYear { get; set; }
    public int SourceMonth { get; set; }
    public IReadOnlyList<MonthPlanExpenseSuggestionDto> Suggestions { get; set; } = [];
}
