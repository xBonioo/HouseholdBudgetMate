namespace HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;

public class CreateTagRequest
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = null!;
}