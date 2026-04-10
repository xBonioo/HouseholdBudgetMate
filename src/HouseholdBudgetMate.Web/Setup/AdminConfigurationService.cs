using System.Text.Json;
using HouseholdBudgetMate.Abstractions.Contracts.Admin.Responses;

namespace HouseholdBudgetMate.Web.Setup;

public interface IAdminConfigurationService
{
    Task<string> ReadConfigurationJsonAsync(CancellationToken cancellationToken);
    Task<AdminConfigurationSaveResult> SaveConfigurationJsonAsync(string json, CancellationToken cancellationToken);
}

public sealed class AdminConfigurationService(RuntimeConfigurationState runtimeConfigurationState) : IAdminConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

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
            return AdminConfigurationSaveResult.Failed("Plik config.json nie może byc pusty.");
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
            return AdminConfigurationSaveResult.Failed($"Nie mozna zapisac pliku config.json: {ex.Message}");
        }
    }
}