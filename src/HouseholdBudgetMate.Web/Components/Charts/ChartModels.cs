namespace HouseholdBudgetMate.Web.Components.Charts;

/// <summary>A single Chart.js dataset descriptor passed to <see cref="ChartCanvas"/>.</summary>
/// <param name="Label">Series label shown in the legend / tooltip.</param>
/// <param name="Data">Data points (converted to double[] on the JS side).</param>
/// <param name="BackgroundColor">Optional explicit hex colour. Null = auto-assign from palette.</param>
/// <param name="BorderColor">Optional explicit border colour. Null = same as BackgroundColor.</param>
/// <param name="Type">Chart.js dataset type: "bar", "line", "pie". Defaults to "bar".</param>
public sealed record ChartDataset(
    string Label,
    decimal[] Data,
    string? BackgroundColor = null,
    string? BorderColor = null,
    string Type = "bar",
    string[]? BackgroundColors = null);
