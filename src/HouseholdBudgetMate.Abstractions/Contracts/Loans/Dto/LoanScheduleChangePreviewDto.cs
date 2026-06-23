namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;

public sealed class LoanScheduleChangePreviewDto
{
    public int LoanId { get; set; }
    public string LoanName { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public string ChangeLabel { get; set; } = string.Empty;
    public DateOnly AffectedFrom { get; set; }
    public string SourceVersion { get; set; } = string.Empty;
    public LoanScheduleSummaryDto BeforeSummary { get; set; } = new();
    public LoanScheduleSummaryDto AfterSummary { get; set; } = new();
    public IReadOnlyList<LoanScheduleComparisonRowDto> Rows { get; set; } = [];
}
