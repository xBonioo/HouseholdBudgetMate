using MudBlazor;

namespace HouseholdBudgetMate.Web.Services;

public static class RemainingBudgetColorResolver
{
    public static Color Resolve(decimal remainingTotal, double remainingPercent)
    {
        if (remainingTotal <= 0)
        {
            return Color.Error;
        }

        if (remainingPercent <= 20)
        {
            return Color.Warning;
        }

        return Color.Success;
    }
}