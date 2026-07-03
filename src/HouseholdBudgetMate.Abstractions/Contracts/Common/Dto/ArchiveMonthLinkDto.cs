namespace HouseholdBudgetMate.Abstractions.Contracts.Common.Dto;

public sealed class ArchiveMonthLinkDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string Label { get; init; } = string.Empty;
}
