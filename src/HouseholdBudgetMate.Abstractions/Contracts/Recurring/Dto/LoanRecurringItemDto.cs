namespace HouseholdBudgetMate.Abstractions.Contracts.Recurring.Dto;

public sealed record LoanRecurringItemDto(string LoanName, string Label, decimal Amount, bool IsPaid);
