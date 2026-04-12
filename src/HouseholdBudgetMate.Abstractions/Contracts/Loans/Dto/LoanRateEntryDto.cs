namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;

public sealed class LoanRateEntryDto
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal ReferenceRate { get; set; }
}