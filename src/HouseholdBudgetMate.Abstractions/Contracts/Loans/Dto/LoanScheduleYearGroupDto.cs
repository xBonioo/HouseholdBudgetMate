namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;

public sealed class LoanScheduleYearGroupDto(
    int year,
    IReadOnlyList<LoanScheduleComparisonRowDto> rows,
    decimal beforeTotal,
    decimal afterTotal,
    bool isExpanded)
{
    public int Year { get; } = year;
    public IReadOnlyList<LoanScheduleComparisonRowDto> Rows { get; } = rows;
    public decimal BeforeTotal { get; } = beforeTotal;
    public decimal AfterTotal { get; } = afterTotal;
    public int RowCount => Rows.Count;
    public bool IsExpanded { get; set; } = isExpanded;
}
