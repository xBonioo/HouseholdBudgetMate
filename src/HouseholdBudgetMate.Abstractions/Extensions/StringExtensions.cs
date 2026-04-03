namespace HouseholdBudgetMate.Abstractions.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Trims the input and returns it with the first character uppercased
    /// and the remaining characters lowercased.
    /// </summary>
    /// <param name="value">Input string to capitalize.</param>
    /// <returns>
    /// Capitalized value, or the original value when input is null, empty, or whitespace.
    /// </returns>
    public static string ToCapitalized(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        value = value.Trim();
        return char.ToUpper(value[0]) + value.Substring(1).ToLower();
    }

    /// <summary>
    /// Converts a PascalCase name to camelCase using the same casing behavior
    /// as the internal JsonCamelCaseNamingPolicy implementation.
    /// </summary>
    /// <param name="name">Source name to convert.</param>
    /// <returns>
    /// camelCase value when conversion is needed; otherwise returns the original input.
    /// </returns>
    public static string ToCamelCase(this string name)
    {
        if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0]))
        {
            return name;
        }

        var chars = name.ToCharArray();
        FixCasing(chars);
        return new string(chars);
    }

    /// <summary>
    /// Adjusts the leading uppercase sequence according to camelCase rules.
    /// </summary>
    /// <param name="chars">Character buffer to mutate in place.</param>
    private static void FixCasing(Span<char> chars)
    {
        for (var i = 0; i < chars.Length; i++)
        {
            if (i == 1 && !char.IsUpper(chars[i]))
            {
                break;
            }

            var hasNext = i + 1 < chars.Length;

            // Stop when next char is already lowercase.
            if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
            {
                // If the next char is a space, lowercase current char before exiting.
                if (chars[i + 1] == ' ')
                {
                    chars[i] = char.ToLowerInvariant(chars[i]);
                }

                break;
            }

            chars[i] = char.ToLowerInvariant(chars[i]);
        }
    }
}