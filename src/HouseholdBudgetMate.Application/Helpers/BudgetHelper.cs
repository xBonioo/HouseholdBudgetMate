using System.Globalization;
using HouseholdBudgetMate.Application.Kernel.Exceptions;

namespace HouseholdBudgetMate.Application.Helpers;

public static class BudgetHelper
{
    public static string NormalizeField(string fieldName, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException($"{fieldName} is required.");
        }

        return value.Trim().ToUpperInvariant();
    }
    
    public static string GetMonthName(int month)
    {
        return new DateTime(2000, month, 1).ToString("MMMM", new CultureInfo("pl-PL"));
    }
    
    public static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new BadRequestException("Income amount must be greater than zero.");
        }
    }

    public static void ValidateDayOfMonth(int dayOfMonth)
    {
        if (dayOfMonth is < 1 or > 31)
        {
            throw new BadRequestException("Day of month must be in range 1..31.");
        }
    }
    
    public static void ValidateMonth(int year, int month)
    {
        if (year is < 2000 or > 3000)
        {
            throw new BadRequestException("Year is out of allowed range.");
        }

        if (month is < 1 or > 12)
        {
            throw new BadRequestException("Month must be in range 1..12.");
        }
    }
}