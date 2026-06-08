using System.Text.Json;
using HouseholdBudgetMate.Abstractions.Contracts.Admin.Responses;
using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Web.Setup;

public interface IAdminConfigurationService
{
    Task<string> ReadConfigurationJsonAsync(CancellationToken cancellationToken);
    Task<AdminConfigurationSaveResult> SaveConfigurationJsonAsync(string json, CancellationToken cancellationToken);
    Task<AdminConfigurationSaveResult> SaveDatabaseConfigurationAsync(
        RuntimeDatabaseConfiguration databaseConfiguration,
        CancellationToken cancellationToken);
    Task<AdminConfigurationSaveResult> SaveHouseholdModeAsync(HouseholdMode householdMode, CancellationToken cancellationToken);
    Task<AdminConfigurationSaveResult> SaveSharingUsersAsync(
        IReadOnlyList<string> sharedWithUserIds,
        CancellationToken cancellationToken);
}

public sealed class AdminConfigurationService(
    RuntimeConfigurationState runtimeConfigurationState,
    IDatabaseMigrationOrchestrator databaseMigrationOrchestrator) : IAdminConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = RuntimeConfigurationState.JsonOptions;

    public async Task<string> ReadConfigurationJsonAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(runtimeConfigurationState.ConfigFilePath))
        {
            var emptyConfiguration = new RuntimeConfigurationState.RuntimeAppConfiguration();
            return JsonSerializer.Serialize(emptyConfiguration, JsonOptions);
        }

        return await File.ReadAllTextAsync(runtimeConfigurationState.ConfigFilePath, cancellationToken);
    }

    public async Task<AdminConfigurationSaveResult> SaveConfigurationJsonAsync(string json, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return AdminConfigurationSaveResult.Failed("Plik config.json nie może być pusty.");
        }

        JsonDocument? parsedDocument;
        try
        {
            parsedDocument = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return AdminConfigurationSaveResult.Failed($"Niepoprawny JSON: {ex.Message}");
        }

        var normalizedJson = string.Empty;
        using (parsedDocument)
        {
            if (parsedDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return AdminConfigurationSaveResult.Failed("Konfiguracja musi być obiektem JSON.");
            }

            normalizedJson = JsonSerializer.Serialize(parsedDocument.RootElement, JsonOptions);
        }

        try
        {
            var directory = Path.GetDirectoryName(runtimeConfigurationState.ConfigFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(runtimeConfigurationState.ConfigFilePath, normalizedJson, cancellationToken);
            runtimeConfigurationState.ReloadFromDisk();
            return AdminConfigurationSaveResult.Success();
        }
        catch (Exception ex)
        {
            return AdminConfigurationSaveResult.Failed($"Nie można zapisać pliku config.json: {ex.Message}");
        }
    }

    public async Task<AdminConfigurationSaveResult> SaveHouseholdModeAsync(
        HouseholdMode householdMode,
        CancellationToken cancellationToken)
    {
        var configuration = await ReadTypedConfigurationAsync(cancellationToken);

        var updatedConfiguration = new RuntimeConfigurationState.RuntimeAppConfiguration
        {
            Database = configuration.Database,
            HouseholdMode = householdMode,
            SharedWithUserIds = configuration.SharedWithUserIds,
            LocalAccessRecoveryEnabled = configuration.LocalAccessRecoveryEnabled,
            BackupSettings = configuration.BackupSettings
        };

        return await WriteTypedConfigurationAsync(
            updatedConfiguration,
            "Nie można zapisać trybu budżetu.",
            cancellationToken);
    }

    public async Task<AdminConfigurationSaveResult> SaveDatabaseConfigurationAsync(
        RuntimeDatabaseConfiguration databaseConfiguration,
        CancellationToken cancellationToken)
    {
        try
        {
            await databaseMigrationOrchestrator.ValidateConnectionAndMigrateAsync(databaseConfiguration, cancellationToken);
        }
        catch (Exception ex)
        {
            return AdminConfigurationSaveResult.Failed($"Nie można połączyć się z bazą lub wykonać migracji: {ex.Message}");
        }

        var configuration = await ReadTypedConfigurationAsync(cancellationToken);
        var updatedConfiguration = new RuntimeConfigurationState.RuntimeAppConfiguration
        {
            Database = databaseConfiguration,
            HouseholdMode = configuration.HouseholdMode,
            SharedWithUserIds = configuration.SharedWithUserIds,
            LocalAccessRecoveryEnabled = configuration.LocalAccessRecoveryEnabled,
            BackupSettings = configuration.BackupSettings
        };

        return await WriteTypedConfigurationAsync(
            updatedConfiguration,
            "Nie można zapisać konfiguracji bazy.",
            cancellationToken);
    }

    public async Task<AdminConfigurationSaveResult> SaveSharingUsersAsync(
        IReadOnlyList<string> sharedWithUserIds,
        CancellationToken cancellationToken)
    {
        var configuration = await ReadTypedConfigurationAsync(cancellationToken);
        var normalizedUserIds = RuntimeConfigurationState.NormalizeUserIds(sharedWithUserIds);

        var updatedConfiguration = new RuntimeConfigurationState.RuntimeAppConfiguration
        {
            Database = configuration.Database,
            HouseholdMode = configuration.HouseholdMode,
            SharedWithUserIds = normalizedUserIds,
            LocalAccessRecoveryEnabled = configuration.LocalAccessRecoveryEnabled,
            BackupSettings = configuration.BackupSettings
        };

        return await WriteTypedConfigurationAsync(
            updatedConfiguration,
            "Nie można zapisać udostępniania budżetu.",
            cancellationToken);
    }

    private async Task<RuntimeConfigurationState.RuntimeAppConfiguration> ReadTypedConfigurationAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(runtimeConfigurationState.ConfigFilePath))
        {
            return new RuntimeConfigurationState.RuntimeAppConfiguration();
        }

        var json = await File.ReadAllTextAsync(runtimeConfigurationState.ConfigFilePath, cancellationToken);
        return JsonSerializer.Deserialize<RuntimeConfigurationState.RuntimeAppConfiguration>(json, JsonOptions)
               ?? new RuntimeConfigurationState.RuntimeAppConfiguration();
    }

    private async Task<AdminConfigurationSaveResult> WriteTypedConfigurationAsync(
        RuntimeConfigurationState.RuntimeAppConfiguration configuration,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var normalizedJson = JsonSerializer.Serialize(configuration, JsonOptions);
        try
        {
            var directory = Path.GetDirectoryName(runtimeConfigurationState.ConfigFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(runtimeConfigurationState.ConfigFilePath, normalizedJson, cancellationToken);
            runtimeConfigurationState.ReloadFromDisk();
            return AdminConfigurationSaveResult.Success();
        }
        catch (Exception ex)
        {
            return AdminConfigurationSaveResult.Failed($"{failureMessage} {ex.Message}");
        }
    }
}
