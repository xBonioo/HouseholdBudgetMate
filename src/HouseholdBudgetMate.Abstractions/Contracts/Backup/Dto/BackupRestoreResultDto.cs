namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

public sealed class BackupRestoreResultDto
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? PreRestoreBackupPath { get; init; }
    public IReadOnlyDictionary<string, int> RestoredCounts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
