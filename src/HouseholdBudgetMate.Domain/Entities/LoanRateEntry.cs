using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class LoanRateEntry : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal ReferenceRate { get; set; }

    public Loan Loan { get; set; } = null!;
}