namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

public sealed class BackupRestorePreviewDto
{
    public string FileName { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, int> CountsByTable { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public bool IsAllowed { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
