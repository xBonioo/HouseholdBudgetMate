using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;

public class ApplyLoanPrepaymentRequest
{
    public int LoanInstallmentId { get; set; }
    public decimal Amount { get; set; }
    public LoanPrepaymentStrategyType Strategy { get; set; }
}