using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto;

namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class MonthlyFinancialPictureDto
{
    public MonthPlanDto MonthPlan { get; set; } = new();
    public LiveBalanceDto LiveBalance { get; set; } = new();

    public int Year => MonthPlan.Year;
    public int Month => MonthPlan.Month;
    public bool IsClosed => MonthPlan.IsClosed;
    public MonthPlanKpiDto Kpi => MonthPlan.Kpi;
    public IReadOnlyList<MonthSavingsTransferItemDto> SavingsTransfers => MonthPlan.SavingsTransfers;
    public IReadOnlyList<ExpenseDto> Expenses => MonthPlan.Expenses;
    public bool HasCompleteBalanceBase => LiveBalance.HasCompleteBalanceBase;
    public IReadOnlyList<string> MissingBalanceAccountNames => LiveBalance.MissingBalanceAccountNames;
}
