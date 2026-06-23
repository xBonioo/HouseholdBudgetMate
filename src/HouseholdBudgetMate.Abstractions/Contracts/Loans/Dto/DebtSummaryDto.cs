namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;

public sealed class DebtSummaryDto
{
    public decimal ActiveDebt { get; set; }
    public int ActiveLoanCount { get; set; }
}
