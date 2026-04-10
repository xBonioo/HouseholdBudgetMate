namespace HouseholdBudgetMate.Abstractions.Contracts.Admin.Responses;

public sealed class AdminConfigurationSaveResult
{
    public bool IsSuccess { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;

    public static AdminConfigurationSaveResult Success()
    {
        return new AdminConfigurationSaveResult { IsSuccess = true };
    }

    public static AdminConfigurationSaveResult Failed(string errorMessage)
    {
        return new AdminConfigurationSaveResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}