using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class LoanPrepayment : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public DateOnly PrepaymentDate { get; set; }
    public decimal Amount { get; set; }

    public Loan Loan { get; set; } = null!;
}
