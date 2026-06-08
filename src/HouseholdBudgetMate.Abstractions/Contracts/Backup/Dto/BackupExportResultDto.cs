namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

public sealed class BackupExportResultDto
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public byte[] Content { get; init; } = [];
    public string? WrittenPath { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
