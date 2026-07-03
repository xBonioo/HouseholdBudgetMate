namespace HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;

public class DeleteTagRequest
{
    public int Id { get; set; }
    public int? ReplacementTagId { get; set; }
    public bool ClearAssignments { get; set; }
}
