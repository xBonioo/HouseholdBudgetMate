namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed record UpcomingInstallmentItemDto(
    string LoanName,
    int Year,
    int Month,
    DateOnly DueDate,
    decimal Amount,
    bool IsPaid);
