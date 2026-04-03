using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace HouseholdBudgetMate.Abstractions.Extensions;

public static class EnumExtensions
{
    /// <summary>
    /// Returns the display name defined by <see cref="DisplayAttribute"/> for an enum value,
    /// or falls back to the enum value name when no attribute is present.
    /// </summary>
    /// <param name="value">Enum value for which the display name is resolved.</param>
    /// <returns>Display name from attribute or the enum value name.</returns>
    public static string GetDisplayName(this Enum value)
    {
        return value
                   .GetType()
                   .GetMember(value.ToString())
                   .First()
                   .GetCustomAttribute<DisplayAttribute>()?
                   .Name
               ?? value.ToString();
    }
}