using HouseholdBudgetMate.Abstractions.Contracts.Backup;

namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

public sealed class BackupRestorePreviewDto
{
    public string FileName { get; init; } = string.Empty;
    public BackupSection IncludedSections { get; init; } = BackupSection.None;
    public BackupSection RestorableSections { get; init; } = BackupSection.None;
    public IReadOnlyList<BackupRestoreUserScopeDto> Users { get; init; } = [];
    public IReadOnlyDictionary<string, int> CountsByTable { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public bool IsAllowed { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
