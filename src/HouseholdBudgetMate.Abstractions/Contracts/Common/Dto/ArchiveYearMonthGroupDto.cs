namespace HouseholdBudgetMate.Abstractions.Contracts.Common.Dto;

public sealed class ArchiveYearMonthGroupDto
{
    public int Year { get; init; }
    public IReadOnlyList<ArchiveMonthLinkDto> Months { get; init; } = [];
}
