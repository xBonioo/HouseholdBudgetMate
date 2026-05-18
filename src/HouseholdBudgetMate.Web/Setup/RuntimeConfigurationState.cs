using System.Text.Json;
using System.Text.Json.Serialization;
using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Web.Setup;

public sealed class RuntimeConfigurationState
{
    private const string ConfigFileName = "config.json";
    private readonly object _lock = new();

    private RuntimeDatabaseConfiguration? _database;
    private HouseholdMode _householdMode = HouseholdMode.SharedBudget;
    private IReadOnlyList<string> _sharedWithUserIds = [];

    public RuntimeConfigurationState(string baseDirectory)
    {
        BaseDirectory = baseDirectory;
        ConfigFilePath = Path.Combine(baseDirectory, ConfigFileName);
        LoadFromDisk();
    }

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

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

    public HouseholdMode GetHouseholdMode()
    {
        lock (_lock)
        {
            return _householdMode;
        }
    }

    public IReadOnlyList<string> GetSharedWithUserIds()
    {
        lock (_lock)
        {
            return _sharedWithUserIds;
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

    public void SetHouseholdMode(HouseholdMode householdMode)
    {
        lock (_lock)
        {
            _householdMode = householdMode;
        }
    }

    public void SetSharedWithUserIds(IReadOnlyList<string> sharedWithUserIds)
    {
        lock (_lock)
        {
            _sharedWithUserIds = sharedWithUserIds;
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
        _householdMode = HouseholdMode.SharedBudget;
        _sharedWithUserIds = [];

        if (!File.Exists(ConfigFilePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize<RuntimeAppConfiguration>(json, JsonOptions);

            if (config?.Database is null
                || string.IsNullOrWhiteSpace(config.Database.Host)
                || string.IsNullOrWhiteSpace(config.Database.Username)
                || string.IsNullOrWhiteSpace(config.Database.Database))
            {
                return;
            }

            _database = config.Database;
            _householdMode = config.HouseholdMode;
            _sharedWithUserIds = NormalizeUserIds(config.SharedWithUserIds);
        }
        catch
        {
            return;
        }
    }

    public sealed class RuntimeAppConfiguration
    {
        public RuntimeDatabaseConfiguration Database { get; init; } = new();
        public HouseholdMode HouseholdMode { get; init; } = HouseholdMode.SharedBudget;
        public IReadOnlyList<string> SharedWithUserIds { get; init; } = [];
    }

    public static IReadOnlyList<string> NormalizeUserIds(IEnumerable<string>? userIds)
    {
        return userIds?
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }
}
