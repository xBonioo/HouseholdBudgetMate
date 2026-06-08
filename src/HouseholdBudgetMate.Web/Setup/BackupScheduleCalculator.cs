using HouseholdBudgetMate.Abstractions.Contracts.Backup;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

namespace HouseholdBudgetMate.Web.Setup;

public static class BackupScheduleCalculator
{
    public static bool IsDue(BackupSettingsDto settings, DateTimeOffset localNow)
    {
        if (!settings.IsEnabled)
        {
            return false;
        }

        var scheduledToday = new DateTimeOffset(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            settings.LocalTime.Hour,
            settings.LocalTime.Minute,
            0,
            localNow.Offset);

        if (localNow < scheduledToday)
        {
            return false;
        }

        if (!settings.LastRunAtUtc.HasValue)
        {
            return true;
        }

        var lastRunLocal = new DateTimeOffset(DateTime.SpecifyKind(settings.LastRunAtUtc.Value, DateTimeKind.Utc))
            .ToOffset(localNow.Offset);

        return settings.Frequency switch
        {
            BackupScheduleFrequency.Daily => lastRunLocal.Date < scheduledToday.Date,
            BackupScheduleFrequency.Weekly => lastRunLocal.Date <= scheduledToday.Date.AddDays(-7),
            BackupScheduleFrequency.Monthly => lastRunLocal.Year < scheduledToday.Year
                                                || lastRunLocal.Year == scheduledToday.Year
                                                && lastRunLocal.Month < scheduledToday.Month,
            _ => false
        };
    }
}
