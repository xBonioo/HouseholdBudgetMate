namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;

public class SetLoanInstallmentPaidRequest
{
    public int LoanInstallmentId { get; set; }
    public bool IsPaid { get; set; }
}