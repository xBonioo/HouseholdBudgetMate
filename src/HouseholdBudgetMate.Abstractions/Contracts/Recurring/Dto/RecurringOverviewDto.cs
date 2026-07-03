namespace HouseholdBudgetMate.Abstractions.Contracts.Recurring.Dto;

public sealed record RecurringOverviewDto(
    decimal ActiveIncomeAmount = 0,
    decimal ActiveExpenseAmount = 0,
    decimal NetRecurringAmount = 0,
    int ActiveIncomeCount = 0,
    int ActiveExpenseCount = 0);
