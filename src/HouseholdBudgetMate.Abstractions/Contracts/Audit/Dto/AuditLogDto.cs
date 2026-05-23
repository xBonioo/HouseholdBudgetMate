namespace HouseholdBudgetMate.Abstractions.Contracts.Audit.Dto;

public sealed class AuditLogDto
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string BudgetOwnerUserId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string EntityContext { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
    public IReadOnlyList<AuditDiffItemDto> DiffItems { get; set; } = [];
}
