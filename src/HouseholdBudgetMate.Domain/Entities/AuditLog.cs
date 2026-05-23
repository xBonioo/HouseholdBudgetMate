using System.ComponentModel.DataAnnotations.Schema;
using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public sealed class AuditLog : ATimestampable
{
    public int Id { get; set; }
    public string EntityType { get; set; } = null!;
    public int EntityId { get; set; }
    public string UserId { get; set; } = User.DefaultUserId;
    public string BudgetOwnerUserId { get; set; } = User.DefaultUserId;
    public string Operation { get; set; } = null!;
    [Column(TypeName = "jsonb")]
    public string OldValuesJson { get; set; } = "{}";
    [Column(TypeName = "jsonb")]
    public string NewValuesJson { get; set; } = "{}";
    public DateTime ChangedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public User BudgetOwnerUser { get; set; } = null!;
}
