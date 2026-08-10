using HouseholdBudgetMate.Abstractions.Contracts.Backup;

namespace HouseholdBudgetMate.Application.Services.Backup;

internal sealed class BackupRestoreSelection
{
    public BackupRestoreSelection(
        BackupSection sections,
        IReadOnlyDictionary<string, BackupSection> userSections)
    {
        Sections = sections;
        UserSections = userSections;
        HasUserScope = userSections.Count > 0;
        ProfileUserIds = BuildUserSet(BackupSection.Profiles);
        BudgetOwnerUserIds = BuildUserSet(BackupSection.Budget);
        AuditBudgetOwnerUserIds = BuildUserSet(BackupSection.Audit);
    }

    public BackupSection Sections { get; }
    public IReadOnlyDictionary<string, BackupSection> UserSections { get; }
    public bool HasUserScope { get; }
    public IReadOnlySet<string> ProfileUserIds { get; }
    public IReadOnlySet<string> BudgetOwnerUserIds { get; }
    public IReadOnlySet<string> AuditBudgetOwnerUserIds { get; }

    public bool IncludesProfile(string userId)
        => !HasUserScope || ProfileUserIds.Contains(userId);

    public bool IncludesBudgetOwner(string userId)
        => !HasUserScope || BudgetOwnerUserIds.Contains(userId);

    public bool IncludesAuditOwner(string userId)
        => !HasUserScope || AuditBudgetOwnerUserIds.Contains(userId);

    private IReadOnlySet<string> BuildUserSet(BackupSection section)
    {
        return UserSections
            .Where(x => x.Value.HasFlag(section))
            .Select(x => x.Key)
            .ToHashSet(StringComparer.Ordinal);
    }
}
