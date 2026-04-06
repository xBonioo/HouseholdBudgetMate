namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class MonthPlanDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IsClosed { get; set; }
    public MonthPlanKpiDto Kpi { get; set; } = new();
    public IReadOnlyList<MonthSavingsTransferItemDto> SavingsTransfers { get; set; } = [];
    public IReadOnlyList<ExpenseDto> Expenses { get; set; } = [];
}