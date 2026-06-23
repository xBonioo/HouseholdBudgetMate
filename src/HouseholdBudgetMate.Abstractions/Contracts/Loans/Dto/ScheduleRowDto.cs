namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;


public sealed record ScheduleRowDto(
    int Year,
    int Month,
    DateOnly DueDate,
    decimal Amount,
    decimal PrincipalAmount,
    decimal InterestAmount,
    decimal ChargesAmount = 0m);
