namespace HouseholdBudgetMate.Application.Validation.Common;

public sealed record YearMonthRequest(int Year, int Month);

public sealed record DateInMonthRequest(DateOnly Date, int Year, int Month, string ErrorMessage);