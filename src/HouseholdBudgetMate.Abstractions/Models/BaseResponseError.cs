namespace HouseholdBudgetMate.Abstractions.Models;

public class BaseResponseError
{
    public BaseResponseError(string message)
    {
        Message = message;
    }

    public BaseResponseError(string? propertyName, string message, string? code)
    {
        PropertyName = propertyName;
        Message = message;
        Code = code;
    }

    public string? PropertyName { get; set; }
    public string Message { get; set; }
    public string? Code { get; set; }
}