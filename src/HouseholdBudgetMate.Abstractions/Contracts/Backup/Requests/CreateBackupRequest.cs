using HouseholdBudgetMate.Abstractions.Contracts.Backup;

namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Requests;

public class CreateBackupRequest
{
    public BackupSection Sections { get; init; } = BackupSection.Budget;
    public string? DestinationPath { get; init; }
    public bool IncludeAllBudgetOwners { get; init; }
    public int? FromYear { get; init; }
    public int? FromMonth { get; init; }
    public int? ToYear { get; init; }
    public int? ToMonth { get; init; }
}
