namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;

public class AddLoanRateEntryRequest
{
    public int LoanId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal ReferenceRate { get; set; }
}