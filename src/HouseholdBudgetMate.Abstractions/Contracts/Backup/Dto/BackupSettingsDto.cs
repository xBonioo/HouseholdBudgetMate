using HouseholdBudgetMate.Abstractions.Contracts.Backup;

namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

public sealed class BackupSettingsDto
{
    public bool IsEnabled { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public BackupScheduleFrequency Frequency { get; set; } = BackupScheduleFrequency.Daily;
    public TimeOnly LocalTime { get; set; } = new(2, 0);
    public BackupSection Sections { get; set; } = BackupSection.FullApp;
    public DateTime? LastRunAtUtc { get; set; }
    public string? LastStatus { get; set; }
}
