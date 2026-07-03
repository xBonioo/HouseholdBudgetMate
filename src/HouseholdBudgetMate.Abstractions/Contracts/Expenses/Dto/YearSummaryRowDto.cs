namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed record YearSummaryRowDto(string Label, decimal Total, decimal MonthlyAverage);
