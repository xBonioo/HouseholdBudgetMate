namespace HouseholdBudgetMate.Abstractions.Contracts.Users.Requests;

public class UpdateUserAdminRoleRequest
{
    public string UserId { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}
