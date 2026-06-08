using HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Requests;

namespace HouseholdBudgetMate.Abstractions.Interfaces;

public interface IBackupSettingsStore
{
    Task<BackupSettingsDto> GetAsync(CancellationToken cancellationToken);
    Task<BackupSettingsDto> SaveAsync(SaveBackupSettingsRequest request, CancellationToken cancellationToken);
    Task<BackupSettingsDto> RecordRunAsync(DateTime utcNow, string status, CancellationToken cancellationToken);
}
