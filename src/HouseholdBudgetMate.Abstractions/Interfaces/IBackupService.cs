using HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Requests;

namespace HouseholdBudgetMate.Abstractions.Interfaces;

public interface IBackupService
{
    Task<CsvExportResultDto> ExportCsvAsync(ExportCsvRequest request, CancellationToken cancellationToken);
    Task<BackupExportResultDto> CreateBackupAsync(CreateBackupRequest request, CancellationToken cancellationToken);
    Task<BackupValidationResultDto> ValidateBackupAsync(Stream content, CancellationToken cancellationToken);
    Task<BackupRestorePreviewDto> PreviewRestoreAsync(Stream content, string fileName, CancellationToken cancellationToken);
    Task<BackupRestoreResultDto> RestoreBackupAsync(RestoreBackupRequest request, CancellationToken cancellationToken);
    Task<BackupSettingsDto> GetBackupSettingsAsync(CancellationToken cancellationToken);
    Task<BackupSettingsDto> SaveBackupSettingsAsync(SaveBackupSettingsRequest request, CancellationToken cancellationToken);
    Task<BackupExportResultDto> RunScheduledBackupNowAsync(CancellationToken cancellationToken);
}
