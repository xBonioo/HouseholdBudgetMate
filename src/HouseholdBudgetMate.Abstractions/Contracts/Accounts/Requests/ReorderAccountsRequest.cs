namespace HouseholdBudgetMate.Abstractions.Contracts.Accounts.Requests;

public class ReorderAccountsRequest
{
    public IReadOnlyList<int> AccountIds { get; set; } = [];
}