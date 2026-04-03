namespace HouseholdBudgetMate.Abstractions.Models;

/// <summary>
///     Key value dictionary (string to string).
/// </summary>
public class StringStringKvp
{
    public StringStringKvp(string? key, string? value = null)
    {
        Key = key;
        Value = value;
    }

    public string? Key { get; set; }
    public string? Value { get; set; }
}