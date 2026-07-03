using System.ComponentModel.DataAnnotations.Schema;
using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class LoanOperationAudit : ATimestampable
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public string UserId { get; set; } = User.DefaultUserId;
    public string BudgetOwnerUserId { get; set; } = User.DefaultUserId;
    public string OperationType { get; set; } = string.Empty;
    public string Status { get; set; } = LoanOperationAuditStatuses.Active;
    public DateTime OccurredAtUtc { get; set; }
    public string ScheduleVersionBefore { get; set; } = string.Empty;
    public string ScheduleVersionAfter { get; set; } = string.Empty;
    [Column(TypeName = "jsonb")]
    public string OperationPayloadJson { get; set; } = "{}";
    public DateTime? RevertedAtUtc { get; set; }
    public string? RevertedByUserId { get; set; }
    public int? RevertsOperationId { get; set; }
    public int? RevertedByOperationId { get; set; }

    public Loan Loan { get; set; } = null!;
    public User User { get; set; } = null!;
    public User BudgetOwnerUser { get; set; } = null!;
    public User? RevertedByUser { get; set; }
    public LoanOperationAudit? RevertsOperation { get; set; }
    public LoanOperationAudit? RevertedByOperation { get; set; }
}
