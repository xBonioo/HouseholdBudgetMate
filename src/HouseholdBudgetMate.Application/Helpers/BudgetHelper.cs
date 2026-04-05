using System.Globalization;

namespace HouseholdBudgetMate.Application.Helpers;

public static class BudgetHelper
{
    public static string GetMonthName(int month)
    {
        return new DateTime(2000, month, 1).ToString("MMMM", new CultureInfo("pl-PL"));
    }
}