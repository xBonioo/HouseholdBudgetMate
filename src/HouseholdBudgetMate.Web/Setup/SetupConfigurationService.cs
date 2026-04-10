using System.Text.Json;

namespace HouseholdBudgetMate.Web.Setup;

public interface ISetupConfigurationService
{
    Task<SetupResult> SaveConfigurationAsync(SetupInputModel inputModel, CancellationToken cancellationToken);
}

public sealed class SetupConfigurationService(
    RuntimeConfigurationState runtimeConfigurationState,
    IDatabaseMigrationOrchestrator databaseMigrationOrchestrator) : ISetupConfigurationService
{
    public async Task<SetupResult> SaveConfigurationAsync(SetupInputModel inputModel, CancellationToken cancellationToken)
    {
        var runtimeDatabaseConfiguration = new RuntimeDatabaseConfiguration
        {
            Host = inputModel.Host.Trim(),
            Port = inputModel.Port,
            Username = inputModel.Username.Trim(),
            Password = inputModel.Password,
            Database = inputModel.Database.Trim()
        };

        try
        {
            await databaseMigrationOrchestrator.ValidateConnectionAndMigrateAsync(runtimeDatabaseConfiguration, cancellationToken);
        }
        catch (Exception ex)
        {
            return SetupResult.Failed($"Nie mozna połączyć się z bazą lub wykonać migracji: {ex.Message}");
        }

        var runtimeAppConfiguration = new RuntimeConfigurationState.RuntimeAppConfiguration
        {
            Database = runtimeDatabaseConfiguration
        };

        var json = JsonSerializer.Serialize(runtimeAppConfiguration, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        try
        {
            var directory = Path.GetDirectoryName(runtimeConfigurationState.ConfigFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(runtimeConfigurationState.ConfigFilePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            return SetupResult.Failed($"Nie mozna zapisac pliku config.json: {ex.Message}");
        }

        runtimeConfigurationState.ReloadFromDisk();
        return SetupResult.Success();
    }
}

public sealed class SetupResult
{
    public bool IsSuccess { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;

    public static SetupResult Success()
    {
        return new SetupResult { IsSuccess = true };
    }

    public static SetupResult Failed(string errorMessage)
    {
        return new SetupResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
