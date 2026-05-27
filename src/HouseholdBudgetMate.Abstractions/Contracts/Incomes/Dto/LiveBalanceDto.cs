namespace HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto;

public sealed class LiveBalanceDto
{
    public decimal AccountsBaseTotal { get; set; }
    public decimal IncomesTotal { get; set; }
    public decimal ExpensesTotal { get; set; }
    public decimal SavingsTransfersTotal { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool HasCompleteBalanceBase { get; set; }
    public IReadOnlyList<string> MissingBalanceAccountNames { get; set; } = [];
}
