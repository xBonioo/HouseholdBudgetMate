namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public readonly record struct YearSummaryPeriodDto(
    int StartMonth,
    int EndMonth,
    int MonthsCount);
