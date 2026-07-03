namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;

public class RevertLoanOperationRequest
{
    public int LoanOperationAuditId { get; set; }
    public string? ExpectedScheduleVersion { get; set; }
}
