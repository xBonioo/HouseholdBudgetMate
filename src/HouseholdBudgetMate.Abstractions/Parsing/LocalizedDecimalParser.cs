using System.Globalization;

namespace HouseholdBudgetMate.Abstractions.Parsing;

public static class LocalizedDecimalParser
{
    public static bool TryParse(string? rawValue, out decimal value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        return TryParseCore(rawValue, out value);
    }

    public static bool TryParseOrZero(string? rawValue, out decimal value)
    {
        value = 0;

        return string.IsNullOrWhiteSpace(rawValue)
               || TryParseCore(rawValue, out value);
    }

    public static bool TryParseOptionalNonNegative(string? rawValue, out decimal? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        if (!TryParseCore(rawValue, out var parsedValue) || parsedValue < 0)
        {
            return false;
        }

        value = parsedValue;
        return true;
    }

    private static bool TryParseCore(string rawValue, out decimal value)
    {
        var normalized = rawValue
            .Trim()
            .Replace(" ", string.Empty)
            .Replace('\u00A0'.ToString(), string.Empty)
            .Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
