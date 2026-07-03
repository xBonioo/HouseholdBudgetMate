namespace HouseholdBudgetMate.Abstractions.Contracts.Accounts.Dto;

public sealed record AccountsOverviewDto(
    decimal LiveBalance = 0,
    bool HasCompleteBalanceBase = false,
    IReadOnlyList<string>? MissingBalanceAccountNamesValue = null,
    decimal CheckingBalance = 0,
    decimal SavingsBalance = 0,
    decimal IncomesTotal = 0,
    decimal ExpensesTotal = 0,
    decimal ActiveDebt = 0,
    int OverspentCategoryCount = 0,
    int TotalAccountCount = 0,
    int ActiveAccountCount = 0,
    bool IsMonthClosed = false)
{
    public IReadOnlyList<string> MissingBalanceAccountNames => MissingBalanceAccountNamesValue ?? [];
}
