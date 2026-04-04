namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class UpdateMonthSavingsTransferRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public DateOnly? TransferDate { get; set; }
}