using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;

public sealed class LoanScheduleComparisonRowDto
{
    public DateOnly DueDate { get; set; }
    public LoanScheduleComparisonRowState State { get; set; }
    public bool BeforeIsPaid { get; set; }
    public bool AfterIsPaid { get; set; }
    public ScheduleRowDto? Before { get; set; }
    public ScheduleRowDto? After { get; set; }
}
