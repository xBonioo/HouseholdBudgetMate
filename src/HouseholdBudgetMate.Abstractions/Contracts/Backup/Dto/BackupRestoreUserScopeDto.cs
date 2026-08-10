using HouseholdBudgetMate.Abstractions.Contracts.Backup;

namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

public sealed class BackupRestoreUserScopeDto
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string BudgetOwnerUserId { get; init; } = string.Empty;
    public bool IsAdmin { get; init; }
    public BackupSection AvailableSections { get; init; } = BackupSection.None;
}
