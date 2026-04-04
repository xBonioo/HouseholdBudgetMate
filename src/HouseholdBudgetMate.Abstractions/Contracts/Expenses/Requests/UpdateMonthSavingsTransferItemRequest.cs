namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

public class UpdateMonthSavingsTransferItemRequest
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateOnly TransferDate { get; set; }
}