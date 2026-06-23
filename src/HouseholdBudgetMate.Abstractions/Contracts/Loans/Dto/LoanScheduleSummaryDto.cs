namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;

public sealed class LoanScheduleSummaryDto
{
    public decimal RemainingPrincipal { get; set; }
    public decimal NextInstallment { get; set; }
    public decimal TotalFutureInterest { get; set; }
    public DateOnly EndDate { get; set; }
    public int InstallmentCount { get; set; }
}
