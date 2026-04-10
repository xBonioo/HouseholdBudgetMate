using System.Text.Json;

namespace HouseholdBudgetMate.Web.Setup;

public sealed class RuntimeConfigurationState
{
    private const string ConfigFileName = "config.json";
    private readonly object _lock = new();

    private RuntimeDatabaseConfiguration? _database;

    public RuntimeConfigurationState(string baseDirectory)
    {
        BaseDirectory = baseDirectory;
        ConfigFilePath = Path.Combine(baseDirectory, ConfigFileName);
        LoadFromDisk();
    }

    public string BaseDirectory { get; }
    public string ConfigFilePath { get; }

    public bool IsConfigured
    {
        get
        {
            lock (_lock)
            {
                return _database is not null;
            }
        }
    }

    public RuntimeDatabaseConfiguration? GetDatabaseConfiguration()
    {
        lock (_lock)
        {
            return _database;
        }
    }

    public void ReloadFromDisk()
    {
        lock (_lock)
        {
            LoadFromDiskUnsafe();
        }
    }

    public void SetDatabaseConfiguration(RuntimeDatabaseConfiguration configuration)
    {
        lock (_lock)
        {
            _database = configuration;
        }
    }

    private void LoadFromDisk()
    {
        lock (_lock)
        {
            LoadFromDiskUnsafe();
        }
    }

    private void LoadFromDiskUnsafe()
    {
        _database = null;

        if (!File.Exists(ConfigFilePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize<RuntimeAppConfiguration>(json);

            if (config?.Database is null
                || string.IsNullOrWhiteSpace(config.Database.Host)
                || string.IsNullOrWhiteSpace(config.Database.Username)
                || string.IsNullOrWhiteSpace(config.Database.Database))
            {
                return;
            }

            _database = config.Database;
        }
        catch
        {
            return;
        }
    }

    public sealed class RuntimeAppConfiguration
    {
        public RuntimeDatabaseConfiguration Database { get; init; } = new();
    }
}

