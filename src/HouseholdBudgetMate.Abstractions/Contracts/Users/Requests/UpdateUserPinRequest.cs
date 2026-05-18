namespace HouseholdBudgetMate.Abstractions.Contracts.Users.Requests;

public class UpdateUserPinRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
}
