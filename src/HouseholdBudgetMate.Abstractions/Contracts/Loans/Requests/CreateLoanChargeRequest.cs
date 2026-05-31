using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;

public class CreateLoanChargeRequest
{
    public int LoanId { get; set; }
    public string Name { get; set; } = null!;
    public LoanChargeType ChargeType { get; set; }
    public LoanChargeFrequencyType FrequencyType { get; set; }
    public decimal Amount { get; set; }
    public bool IsPercentageBased { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}