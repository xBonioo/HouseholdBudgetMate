namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

public sealed class BackupEnvelopeDto
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string ApplicationName { get; set; } = "HouseholdBudgetMate";
    public BackupManifestDto Manifest { get; set; } = new();
    public BackupPayloadDto Payload { get; set; } = new();
}
