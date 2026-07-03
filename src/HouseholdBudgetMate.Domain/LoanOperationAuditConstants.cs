namespace HouseholdBudgetMate.Domain;

public static class LoanOperationAuditTypes
{
    public const string LoanPrepayment = "LoanPrepayment";
    public const string LoanRateEntry = "LoanRateEntry";
    public const string LoanOperationRevert = "LoanOperationRevert";
}

public static class LoanOperationAuditStatuses
{
    public const string Active = "Active";
    public const string Reverted = "Reverted";
}
