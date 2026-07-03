namespace HouseholdBudgetMate.Abstractions.Contracts.Recurring.Dto;

public sealed record IncomeDefinitionRowDto(
    int Id,
    string Name,
    decimal Amount,
    int DayOfMonth,
    int AccountId,
    string AccountName,
    bool IsActive);
