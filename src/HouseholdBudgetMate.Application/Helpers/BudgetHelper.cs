using System.Globalization;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Domain.Entities;

namespace HouseholdBudgetMate.Application.Helpers;

public static class BudgetHelper
{
    public static string GetMonthName(int month)
    {
        return new DateTime(2000, month, 1).ToString("MMMM", new CultureInfo("pl-PL"));
    }

    public static void EnsureMonthIsOpen(MonthPlan? monthPlan)
    {
        if (monthPlan is { IsClosed: true })
        {
            throw new BadRequestException("Month is closed. Editing is disabled.");
        }
    }
}