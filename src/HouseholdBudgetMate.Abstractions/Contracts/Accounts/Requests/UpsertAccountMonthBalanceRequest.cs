namespace HouseholdBudgetMate.Abstractions.Contracts.Accounts.Requests;

public class UpsertAccountMonthBalanceRequest
{
    public int AccountId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal ClosingBalance { get; set; }
}