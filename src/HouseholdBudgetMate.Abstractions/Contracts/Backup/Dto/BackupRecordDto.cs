namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

public sealed class BackupRecordDto
{
    public string Table { get; set; } = string.Empty;
    public string PortableId { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string?> Fields { get; set; } = new Dictionary<string, string?>();
    public IReadOnlyDictionary<string, string> References { get; set; } = new Dictionary<string, string>();
}
