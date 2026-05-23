namespace HouseholdBudgetMate.Abstractions.Contracts.Audit.Dto;

public sealed class AuditDiffItemDto
{
    public string PropertyName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
}
