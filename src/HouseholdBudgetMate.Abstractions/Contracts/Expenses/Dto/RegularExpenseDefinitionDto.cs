namespace HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

public sealed class RegularExpenseDefinitionDto
{
    public int Id { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? TagId { get; set; }
    public string? TagName { get; set; }
    public decimal Amount { get; set; }
    public bool IsActive { get; set; }
    public bool ShowRemainingInUI { get; set; } = true;
}