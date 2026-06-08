using HouseholdBudgetMate.Abstractions.Contracts.Backup;

namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

public sealed class BackupManifestDto
{
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByUsername { get; set; } = string.Empty;
    public BackupSection RequestedSections { get; set; } = BackupSection.None;
    public BackupSection IncludedSections { get; set; } = BackupSection.None;
    public int? BudgetFromYear { get; set; }
    public int? BudgetFromMonth { get; set; }
    public int? BudgetToYear { get; set; }
    public int? BudgetToMonth { get; set; }
    public IReadOnlyDictionary<string, int> CountsByTable { get; set; } = new Dictionary<string, int>();
    public IReadOnlyList<string> Warnings { get; set; } = [];
}
