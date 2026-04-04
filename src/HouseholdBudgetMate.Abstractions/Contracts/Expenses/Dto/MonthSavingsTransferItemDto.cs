namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class MonthSavingsTransferItemDto
{
    public int Id { get; set; }
    public int MonthPlanId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly TransferDate { get; set; }
}