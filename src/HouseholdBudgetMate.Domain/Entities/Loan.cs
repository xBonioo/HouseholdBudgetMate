using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class Loan : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public string UserId { get; set; } = User.DefaultUserId;
    public string Name { get; set; } = null!;
    public int LoanType { get; set; }
    public int InterestMode { get; set; }
    public int? WiborPeriodType { get; set; }
    public decimal Principal { get; set; }
    public decimal InterestRate { get; set; }
    public decimal? MarginRate { get; set; }
    public int RepaymentDayOfMonth { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int? TagId { get; set; }
    public bool IsActive { get; set; } = true;

    public Tag? Tag { get; set; }
    public ICollection<LoanRateEntry> RateEntries { get; set; } = new List<LoanRateEntry>();
    public ICollection<LoanCharge> Charges { get; set; } = new List<LoanCharge>();
    public ICollection<LoanInstallment> Installments { get; set; } = new List<LoanInstallment>();
}
