namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;

public class ApplyLoanInstallmentAmountChangeRequest
{
    public int LoanInstallmentId { get; set; }
    public decimal InstallmentAmount { get; set; }
    public DateOnly LastInstallmentDate { get; set; }
    public string? ExpectedScheduleVersion { get; set; }
}
