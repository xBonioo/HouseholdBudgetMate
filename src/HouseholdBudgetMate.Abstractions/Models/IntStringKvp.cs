namespace HouseholdBudgetMate.Abstractions.Models;

/// <summary>
///     Key value dictionary (string to int).
/// </summary>
public class IntStringKvp(int? key, string? value = null)
{
    public int? Key { get; set; } = key;
    public string? Value { get; set; } = value;
}