namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

public sealed class BackupPayloadDto
{
    public BackupRecordSectionDto? Taxonomy { get; set; }
    public BackupRecordSectionDto? Budget { get; set; }
    public BackupRecordSectionDto? Profiles { get; set; }
    public BackupRecordSectionDto? Audit { get; set; }
    public BackupRecordSectionDto? Logs { get; set; }
    public BackupSettingsMetadataSectionDto? SettingsMetadata { get; set; }
}
