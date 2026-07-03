namespace HouseholdBudgetMate.Abstractions.Contracts.Categories.Responses;

public sealed record DeleteReassignResult(
    int? ReplacementCategoryId,
    int? ReplacementTagId,
    bool ClearAssignments);
