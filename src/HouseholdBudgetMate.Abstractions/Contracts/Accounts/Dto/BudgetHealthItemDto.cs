namespace HouseholdBudgetMate.Abstractions.Contracts.Accounts.Dto;

public sealed record BudgetHealthItemDto(
    string Name,
    decimal LimitAmount,
    decimal SpentAmount,
    decimal RemainingAmount);
