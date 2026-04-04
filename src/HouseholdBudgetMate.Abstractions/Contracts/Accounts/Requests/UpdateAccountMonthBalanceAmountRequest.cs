namespace HouseholdBudgetMate.Abstractions.Contracts.Accounts.Requests;

public class UpdateAccountMonthBalanceAmountRequest
{
    public int Id { get; set; }
    public decimal ClosingBalance { get; set; }
}

