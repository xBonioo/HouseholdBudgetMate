namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

public sealed class BackupRecordSectionDto
{
    public IReadOnlyList<BackupRecordDto> Records { get; set; } = [];
}
