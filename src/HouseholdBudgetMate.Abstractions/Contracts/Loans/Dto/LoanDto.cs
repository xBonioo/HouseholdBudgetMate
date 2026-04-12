using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;

public sealed class LoanDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public LoanType LoanType { get; set; }
    public LoanInterestMode InterestMode { get; set; }
    public WiborPeriodType? WiborPeriodType { get; set; }
    public decimal Principal { get; set; }
    public decimal RemainingPrincipal { get; set; }
    public decimal InterestRate { get; set; }
    public decimal? MarginRate { get; set; }
    public decimal? CurrentReferenceRate { get; set; }
    public int RepaymentDayOfMonth { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int? TagId { get; set; }
    public string? TagName { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<LoanRateEntryDto> RateEntries { get; set; } = [];
    public IReadOnlyList<LoanChargeDto> Charges { get; set; } = [];
    public IReadOnlyList<LoanInstallmentDto> Installments { get; set; } = [];
}