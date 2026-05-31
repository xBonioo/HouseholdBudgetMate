namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;

public class OverrideLoanInstallmentRequest
{
    public int InstallmentId { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal ChargesAmount { get; set; }
}
