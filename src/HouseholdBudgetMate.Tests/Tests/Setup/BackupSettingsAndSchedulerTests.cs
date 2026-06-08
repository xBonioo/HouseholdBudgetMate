using HouseholdBudgetMate.Abstractions.Contracts.Backup;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Requests;
using HouseholdBudgetMate.Web.Setup;

namespace HouseholdBudgetMate.Tests.Tests.Setup;

public sealed class BackupSettingsAndSchedulerTests
{
    [Fact]
    public async Task RuntimeBackupSettingsStore_Should_Save_And_Load_Backup_Settings()
    {
        var directory = CreateTempDirectory();
        var state = new RuntimeConfigurationState(directory);
        var store = new RuntimeBackupSettingsStore(state);
        var backupPath = Path.Combine(directory, "custom-backups");

        await store.SaveAsync(
            new SaveBackupSettingsRequest
            {
                IsEnabled = true,
                BackupPath = backupPath,
                Frequency = BackupScheduleFrequency.Weekly,
                LocalTime = new TimeOnly(3, 30),
                Sections = BackupSection.Budget | BackupSection.Taxonomy
            },
            CancellationToken.None);

        var reloaded = new RuntimeConfigurationState(directory);
        var settings = reloaded.GetBackupSettings();

        Assert.True(settings.IsEnabled);
        Assert.Equal(backupPath, settings.BackupPath);
        Assert.Equal(BackupScheduleFrequency.Weekly, settings.Frequency);
        Assert.Equal(new TimeOnly(3, 30), settings.LocalTime);
        Assert.Equal(BackupSection.Budget | BackupSection.Taxonomy, settings.Sections);
    }

    [Fact]
    public async Task RuntimeBackupSettingsStore_Should_Validate_Path_Frequency_And_Sections()
    {
        var directory = CreateTempDirectory();
        var store = new RuntimeBackupSettingsStore(new RuntimeConfigurationState(directory));

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(
            new SaveBackupSettingsRequest
            {
                BackupPath = "valid",
                Frequency = (BackupScheduleFrequency)999,
                Sections = BackupSection.FullApp
            },
            CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(
            new SaveBackupSettingsRequest
            {
                BackupPath = "valid",
                Frequency = BackupScheduleFrequency.Daily,
                Sections = BackupSection.None
            },
            CancellationToken.None));
    }

    [Theory]
    [InlineData(BackupScheduleFrequency.Daily, "2026-06-07T01:59:00+02:00", null, false)]
    [InlineData(BackupScheduleFrequency.Daily, "2026-06-07T02:00:00+02:00", null, true)]
    [InlineData(BackupScheduleFrequency.Daily, "2026-06-07T03:00:00+02:00", "2026-06-06T00:30:00Z", true)]
    [InlineData(BackupScheduleFrequency.Daily, "2026-06-07T03:00:00+02:00", "2026-06-07T00:30:00Z", false)]
    [InlineData(BackupScheduleFrequency.Weekly, "2026-06-08T02:00:00+02:00", "2026-06-01T00:00:00Z", true)]
    [InlineData(BackupScheduleFrequency.Weekly, "2026-06-08T02:00:00+02:00", "2026-06-04T00:00:00Z", false)]
    [InlineData(BackupScheduleFrequency.Monthly, "2026-07-01T02:00:00+02:00", "2026-06-15T00:00:00Z", true)]
    [InlineData(BackupScheduleFrequency.Monthly, "2026-07-20T02:00:00+02:00", "2026-07-01T00:00:00Z", false)]
    public void BackupScheduleCalculator_Should_Detect_Due_Backups(
        BackupScheduleFrequency frequency,
        string localNowText,
        string? lastRunUtcText,
        bool expected)
    {
        var settings = new BackupSettingsDto
        {
            IsEnabled = true,
            Frequency = frequency,
            LocalTime = new TimeOnly(2, 0),
            LastRunAtUtc = lastRunUtcText is null
                ? null
                : DateTime.Parse(lastRunUtcText, null, System.Globalization.DateTimeStyles.RoundtripKind)
        };

        var localNow = DateTimeOffset.Parse(localNowText, null, System.Globalization.DateTimeStyles.RoundtripKind);

        Assert.Equal(expected, BackupScheduleCalculator.IsDue(settings, localNow));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HouseholdBudgetMateTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
