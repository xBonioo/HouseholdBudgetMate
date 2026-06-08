using System.Text.Json;
using System.Text.Json.Serialization;
using HouseholdBudgetMate.Abstractions.Contracts.Backup;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;
using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Web.Setup;

public sealed class RuntimeConfigurationState
{
    private const string ConfigFileName = "config.json";
    private readonly object _lock = new();

    private RuntimeDatabaseConfiguration? _database;
    private HouseholdMode _householdMode = HouseholdMode.SharedBudget;
    private IReadOnlyList<string> _sharedWithUserIds = [];
    private bool _localAccessRecoveryEnabled;
    private BackupSettingsDto _backupSettings;

    public RuntimeConfigurationState(string baseDirectory)
    {
        BaseDirectory = baseDirectory;
        ConfigFilePath = Path.Combine(baseDirectory, ConfigFileName);
        _backupSettings = CreateDefaultBackupSettings(baseDirectory);
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

    public bool IsLocalAccessRecoveryEnabled
    {
        get
        {
            lock (_lock)
            {
                return _localAccessRecoveryEnabled;
            }
        }
    }

    public BackupSettingsDto GetBackupSettings()
    {
        lock (_lock)
        {
            return CloneBackupSettings(_backupSettings);
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

    public void SetBackupSettings(BackupSettingsDto backupSettings)
    {
        lock (_lock)
        {
            _backupSettings = CloneBackupSettings(backupSettings);
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
        _localAccessRecoveryEnabled = false;
        _backupSettings = CreateDefaultBackupSettings(BaseDirectory);

        if (!File.Exists(ConfigFilePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize<RuntimeAppConfiguration>(json, JsonOptions);

            _backupSettings = NormalizeBackupSettings(config?.BackupSettings, BaseDirectory);

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
            _localAccessRecoveryEnabled = config.LocalAccessRecoveryEnabled;
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
        public bool LocalAccessRecoveryEnabled { get; init; }
        public BackupSettingsDto BackupSettings { get; init; } = CreateDefaultBackupSettings(AppContext.BaseDirectory);
    }

    public static IReadOnlyList<string> NormalizeUserIds(IEnumerable<string>? userIds)
    {
        return userIds?
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    public static BackupSettingsDto CreateDefaultBackupSettings(string baseDirectory)
    {
        return new BackupSettingsDto
        {
            IsEnabled = false,
            BackupPath = Path.Combine(baseDirectory, "backups"),
            Frequency = BackupScheduleFrequency.Daily,
            LocalTime = new TimeOnly(2, 0),
            Sections = BackupSection.FullApp
        };
    }

    public static BackupSettingsDto NormalizeBackupSettings(
        BackupSettingsDto? settings,
        string baseDirectory)
    {
        var defaults = CreateDefaultBackupSettings(baseDirectory);
        if (settings is null)
        {
            return defaults;
        }

        return new BackupSettingsDto
        {
            IsEnabled = settings.IsEnabled,
            BackupPath = string.IsNullOrWhiteSpace(settings.BackupPath)
                ? defaults.BackupPath
                : settings.BackupPath.Trim(),
            Frequency = Enum.IsDefined(settings.Frequency) ? settings.Frequency : defaults.Frequency,
            LocalTime = settings.LocalTime,
            Sections = settings.Sections == BackupSection.None ? defaults.Sections : settings.Sections,
            LastRunAtUtc = settings.LastRunAtUtc,
            LastStatus = settings.LastStatus
        };
    }

    private static BackupSettingsDto CloneBackupSettings(BackupSettingsDto settings)
    {
        return new BackupSettingsDto
        {
            IsEnabled = settings.IsEnabled,
            BackupPath = settings.BackupPath,
            Frequency = settings.Frequency,
            LocalTime = settings.LocalTime,
            Sections = settings.Sections,
            LastRunAtUtc = settings.LastRunAtUtc,
            LastStatus = settings.LastStatus
        };
    }
}
