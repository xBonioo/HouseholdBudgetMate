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
        PaletteDark = new PaletteDark
        {
            Primary             = "#6366F1",
            Secondary           = "#38BDF8",
            Success             = "#34D399",
            Warning             = "#FCD34D",
            Error               = "#F87171",
            Background          = "#0F172A",
            Surface             = "#1E293B",
            AppbarBackground    = "#0F172A",
            AppbarText          = "#F8FAFC",
            TextPrimary         = "#F8FAFC",
            TextSecondary       = "#94A3B8"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px"
        }
    };
}

