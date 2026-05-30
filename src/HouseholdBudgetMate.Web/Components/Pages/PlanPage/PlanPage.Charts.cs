using HouseholdBudgetMate.Web.Components.Charts;

namespace HouseholdBudgetMate.Web.Components.Pages.PlanPage;

public partial class PlanPage
{
    private string[] _pieLabels = [];
    private ChartDataset[] _pieDatasets = [];
    private HashSet<int> _pieSelectedCategoryIds = [];

    private IReadOnlyList<(int Id, string Name)> GetPieAvailableCategories()
    {
        if (_monthPlan is null) return [];
        return _monthPlan.Expenses
            .Where(e => e.ActualAmount > 0)
            .GroupBy(e => new { e.CategoryId, e.CategoryName })
            .Where(g => g.Sum(x => x.ActualAmount) > 0)
            .Select(g => (g.Key.CategoryId, g.Key.CategoryName))
            .OrderBy(x => x.CategoryName)
            .ToList();
    }

    private void OnPieCategoriesChanged(IReadOnlyCollection<int>? ids)
    {
        _pieSelectedCategoryIds = ids?.ToHashSet() ?? [];
        RecomputePieChartData();
    }

    internal void RecomputePieChartData()
    {
        if (_monthPlan is null || _kpi.SpentTotal == 0)
        {
            _pieLabels = [];
            _pieDatasets = [];
            return;
        }

        var grouped = _monthPlan.Expenses
            .Where(e => e.ActualAmount > 0)
            .Where(e => _pieSelectedCategoryIds.Count == 0 || _pieSelectedCategoryIds.Contains(e.CategoryId))
            .GroupBy(e => new { e.CategoryId, e.CategoryName })
            .Select(g => (CategoryId: g.Key.CategoryId, Label: g.Key.CategoryName, Amount: g.Sum(x => x.ActualAmount)))
            .Where(x => x.Amount > 0)
            .OrderByDescending(x => x.Amount)
            .ToList();

        if (grouped.Count == 0)
        {
            _pieLabels = [];
            _pieDatasets = [];
            return;
        }

        var colorMap = _categories.ToDictionary(c => c.Id, c => c.Color);

        _pieLabels = grouped.Select(x => x.Label).ToArray();
        var sliceColors = grouped
            .Select(x => colorMap.GetValueOrDefault(x.CategoryId, "#6366F1"))
            .ToArray();
        _pieDatasets = [new ChartDataset("Wydatki per kategoria", grouped.Select(x => x.Amount).ToArray(), null, null, "pie", sliceColors)];
    }
}
