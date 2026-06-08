using HouseholdBudgetMate.Abstractions.Contracts.Backup;

namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Requests;

public class ExportCsvRequest
{
    public int Year { get; init; }
    public int? Month { get; init; }
    public int? CategoryId { get; init; }
    public int? AccountId { get; init; }
    public bool IncludeDeleted { get; init; }
    public BackupCsvExportKind Kind { get; init; } = BackupCsvExportKind.ExpensesAndIncomes;
}
