namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

public sealed class BackupValidationResultDto
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
