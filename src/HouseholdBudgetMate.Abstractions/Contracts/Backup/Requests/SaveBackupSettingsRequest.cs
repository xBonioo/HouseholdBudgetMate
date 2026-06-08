using HouseholdBudgetMate.Abstractions.Contracts.Backup;

namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Requests;

public class SaveBackupSettingsRequest
{
    public bool IsEnabled { get; init; }
    public string BackupPath { get; init; } = string.Empty;
    public BackupScheduleFrequency Frequency { get; init; } = BackupScheduleFrequency.Daily;
    public TimeOnly LocalTime { get; init; } = new(2, 0);
    public BackupSection Sections { get; init; } = BackupSection.FullApp;
}
