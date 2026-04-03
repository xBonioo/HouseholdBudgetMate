using MudBlazor;

namespace HouseholdBudgetMate.Web.Components.Themes;

public static class AppTheme
{
    public static MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#4F46E5",
            Secondary = "#0EA5E9",
            Success = "#10B981",
            Warning = "#F59E0B",
            Error = "#EF4444",
            Background = "#F8FAFC",
            Surface = "#FFFFFF",

            AppbarBackground = "#111827",
            AppbarText = "#FFFFFF",

            TextPrimary = "#0F172A",
            TextSecondary = "#6B7280"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px"
        }
    };
}

