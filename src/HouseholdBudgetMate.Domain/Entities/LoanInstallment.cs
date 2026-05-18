using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class LoanInstallment : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAtUtc { get; set; }

    public Loan Loan { get; set; } = null!;
    public Expense? Expense { get; set; }
}
