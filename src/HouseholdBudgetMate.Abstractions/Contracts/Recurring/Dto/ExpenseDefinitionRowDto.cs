namespace HouseholdBudgetMate.Abstractions.Contracts.Recurring.Dto;

public sealed record ExpenseDefinitionRowDto(
    int Id,
    int Order,
    string Name,
    int CategoryId,
    string CategoryName,
    int? TagId,
    string? TagName,
    decimal Amount,
    bool IsActive,
    bool ShowRemainingInUI);
