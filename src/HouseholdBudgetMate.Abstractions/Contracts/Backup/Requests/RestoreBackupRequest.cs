namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Requests;

public class RestoreBackupRequest
{
    public Stream Content { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ConfirmationPhrase { get; init; } = string.Empty;
}
