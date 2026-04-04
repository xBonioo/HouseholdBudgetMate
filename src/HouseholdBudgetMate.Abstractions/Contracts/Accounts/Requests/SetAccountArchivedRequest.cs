namespace HouseholdBudgetMate.Abstractions.Contracts.Accounts.Requests;

public class SetAccountArchivedRequest
{
    public int Id { get; set; }
    public bool IsArchived { get; set; }
}