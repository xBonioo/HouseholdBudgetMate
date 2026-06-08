using System.Globalization;
using HouseholdBudgetMate.Abstractions.Contracts.Backup;

namespace HouseholdBudgetMate.Application.Services.Backup;

internal static class BackupFileName
{
    public static string Build(DateTimeOffset createdAtUtc, BackupSection sections)
    {
        var timestamp = createdAtUtc.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return $"household-budget-mate-backup-{timestamp}-{BuildSectionSummary(sections)}.json";
    }

    private static string BuildSectionSummary(BackupSection sections)
    {
        if ((sections & BackupSection.FullApp) == BackupSection.FullApp)
        {
            return "full";
        }

        var names = Enum.GetValues<BackupSection>()
            .Where(section => section is not BackupSection.None and not BackupSection.FullApp)
            .Where(section => sections.HasFlag(section))
            .Select(section => section.ToString().ToLowerInvariant())
            .ToArray();

        return names.Length == 0 ? "empty" : string.Join("-", names);
    }
}
