using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;

public class CreateLoanRequest
{
    public string Name { get; set; } = null!;
    public LoanType LoanType { get; set; }
    public LoanInterestMode InterestMode { get; set; }
    public WiborPeriodType? WiborPeriodType { get; set; }
    public decimal Principal { get; set; }
    public decimal? OriginalPrincipal { get; set; }
    public int? GracePeriodMonths { get; set; }
    public decimal InterestRate { get; set; }
    public decimal? MarginRate { get; set; }
    public DateOnly? InitialRateEffectiveFrom { get; set; }
    public decimal? InitialReferenceRate { get; set; }
    public int RepaymentDayOfMonth { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int? TagId { get; set; }
    public bool IsActive { get; set; } = true;
}