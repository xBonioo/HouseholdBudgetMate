using HouseholdBudgetMate.Abstractions.Contracts.Backup;

namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Requests;

public class RestoreBackupRequest
{
    public Stream Content { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ConfirmationPhrase { get; init; } = string.Empty;
    public BackupSection Sections { get; init; } = BackupSection.None;
    public IReadOnlyDictionary<string, BackupSection> UserSections { get; init; } =
        new Dictionary<string, BackupSection>();
}
