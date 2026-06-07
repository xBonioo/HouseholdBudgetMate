namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class ApplyMonthPlanSuggestionItemRequest
{
    public int SourceExpenseId { get; set; }
    public decimal PlannedAmount { get; set; }
}
