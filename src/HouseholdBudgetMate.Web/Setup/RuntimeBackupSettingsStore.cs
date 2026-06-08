using System.Text.Json;
using HouseholdBudgetMate.Abstractions.Contracts.Backup;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Requests;
using HouseholdBudgetMate.Abstractions.Interfaces;

namespace HouseholdBudgetMate.Web.Setup;

public sealed class RuntimeBackupSettingsStore(RuntimeConfigurationState runtimeConfigurationState) : IBackupSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = RuntimeConfigurationState.JsonOptions;

    public Task<BackupSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(runtimeConfigurationState.GetBackupSettings());
    }

    public async Task<BackupSettingsDto> SaveAsync(
        SaveBackupSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var current = runtimeConfigurationState.GetBackupSettings();
        var settings = RuntimeConfigurationState.NormalizeBackupSettings(
            new BackupSettingsDto
            {
                IsEnabled = request.IsEnabled,
                BackupPath = string.IsNullOrWhiteSpace(request.BackupPath) ? current.BackupPath : request.BackupPath,
                Frequency = request.Frequency,
                LocalTime = request.LocalTime,
                Sections = request.Sections,
                LastRunAtUtc = current.LastRunAtUtc,
                LastStatus = current.LastStatus
            },
            runtimeConfigurationState.BaseDirectory);

        await WriteSettingsAsync(settings, cancellationToken);
        return settings;
    }

    public async Task<BackupSettingsDto> RecordRunAsync(
        DateTime utcNow,
        string status,
        CancellationToken cancellationToken)
    {
        var current = runtimeConfigurationState.GetBackupSettings();
        current.LastRunAtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        current.LastStatus = status;

        await WriteSettingsAsync(current, cancellationToken);
        return current;
    }

    private async Task WriteSettingsAsync(BackupSettingsDto settings, CancellationToken cancellationToken)
    {
        var configuration = await ReadTypedConfigurationAsync(cancellationToken);
        var updated = new RuntimeConfigurationState.RuntimeAppConfiguration
        {
            Database = configuration.Database,
            HouseholdMode = configuration.HouseholdMode,
            SharedWithUserIds = configuration.SharedWithUserIds,
            LocalAccessRecoveryEnabled = configuration.LocalAccessRecoveryEnabled,
            BackupSettings = settings
        };

        var directory = Path.GetDirectoryName(runtimeConfigurationState.ConfigFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            runtimeConfigurationState.ConfigFilePath,
            JsonSerializer.Serialize(updated, JsonOptions),
            cancellationToken);

        runtimeConfigurationState.ReloadFromDisk();
    }

    private async Task<RuntimeConfigurationState.RuntimeAppConfiguration> ReadTypedConfigurationAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(runtimeConfigurationState.ConfigFilePath))
        {
            return new RuntimeConfigurationState.RuntimeAppConfiguration
            {
                BackupSettings = runtimeConfigurationState.GetBackupSettings()
            };
        }

        var json = await File.ReadAllTextAsync(runtimeConfigurationState.ConfigFilePath, cancellationToken);
        return JsonSerializer.Deserialize<RuntimeConfigurationState.RuntimeAppConfiguration>(json, JsonOptions)
               ?? new RuntimeConfigurationState.RuntimeAppConfiguration();
    }

    private static void ValidateRequest(SaveBackupSettingsRequest request)
    {
        if (!Enum.IsDefined(request.Frequency))
        {
            throw new ArgumentException("Backup schedule frequency is invalid.", nameof(request));
        }

        if (request.Sections == BackupSection.None)
        {
            throw new ArgumentException("At least one backup section must be selected.", nameof(request));
        }

        var path = request.BackupPath?.Trim();
        if (!string.IsNullOrWhiteSpace(path) && path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new ArgumentException("Backup path contains invalid characters.", nameof(request));
        }
    }
}
