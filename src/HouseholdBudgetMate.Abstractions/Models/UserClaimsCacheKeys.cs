namespace HouseholdBudgetMate.Abstractions.Models;

public static class UserClaimsCacheKeys
{
    private const string Prefix = "claims_";

    public static string ForLogin(string login)
    {
        if (string.IsNullOrWhiteSpace(login))
            return Prefix;

        return $"{Prefix}{login.Trim().ToUpperInvariant()}";
    }
}