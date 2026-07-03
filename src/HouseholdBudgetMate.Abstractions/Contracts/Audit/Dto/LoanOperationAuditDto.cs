namespace HouseholdBudgetMate.Abstractions.Contracts.Audit.Dto;

public sealed class LoanOperationAuditDto
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public string LoanName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string BudgetOwnerUserId { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OperationContext { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string ScheduleVersionBefore { get; set; } = string.Empty;
    public string ScheduleVersionAfter { get; set; } = string.Empty;
    public string OperationPayloadJson { get; set; } = "{}";
    public DateTime? RevertedAtUtc { get; set; }
    public string? RevertedByUserId { get; set; }
    public string? RevertedByUserName { get; set; }
    public int? RevertsOperationId { get; set; }
    public int? RevertedByOperationId { get; set; }
    public bool CanRevert { get; set; }
    public string? RevertBlockedReason { get; set; }
}
