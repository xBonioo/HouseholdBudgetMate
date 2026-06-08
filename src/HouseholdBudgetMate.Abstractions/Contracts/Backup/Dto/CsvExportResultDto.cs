namespace HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;

public sealed class CsvExportResultDto
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public byte[] Content { get; init; } = [];
    public int ExpenseRowCount { get; init; }
    public int IncomeRowCount { get; init; }
}
