using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class LoanCharge : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public string Name { get; set; } = null!;
    public int ChargeType { get; set; }
    public int FrequencyType { get; set; }
    public decimal Amount { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    public Loan Loan { get; set; } = null!;
}