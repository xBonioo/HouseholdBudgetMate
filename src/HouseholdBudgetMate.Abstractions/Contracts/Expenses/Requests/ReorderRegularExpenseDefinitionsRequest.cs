namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class ReorderRegularExpenseDefinitionsRequest
{
    public IReadOnlyList<int> DefinitionIds { get; set; } = [];
}