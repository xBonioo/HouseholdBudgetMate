namespace HouseholdBudgetMate.Abstractions.Contracts.Backup;

[Flags]
public enum BackupSection
{
    None = 0,
    Budget = 1,
    Taxonomy = 2,
    Profiles = 4,
    Audit = 8,
    Logs = 16,
    SettingsMetadata = 32,
    FullApp = Budget | Taxonomy | Profiles | Audit | Logs | SettingsMetadata
}
