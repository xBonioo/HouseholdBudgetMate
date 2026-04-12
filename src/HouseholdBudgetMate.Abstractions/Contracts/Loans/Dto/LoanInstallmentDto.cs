namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;

public sealed class LoanInstallmentDto
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
    public int? ExpenseId { get; set; }
}