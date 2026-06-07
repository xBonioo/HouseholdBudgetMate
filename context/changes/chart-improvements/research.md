---
date: 2026-06-07T17:31:29.7917825+02:00
researcher: Codex
git_commit: 2636d136fb39b1707691c1663bfd1c96c6806bcf
branch: main
repository: HouseholdBudgetMate
topic: "Research the current chart setup in the app and identify practical improvements or a good new chart to add."
tags: [research, codebase, charts, statistics, dashboard, plan-page]
status: complete
last_updated: 2026-06-07
last_updated_by: Codex
---

# Research: Current chart setup and improvement opportunities

**Date**: 2026-06-07T17:31:29.7917825+02:00
**Researcher**: Codex
**Git Commit**: `2636d136fb39b1707691c1663bfd1c96c6806bcf`
**Branch**: `main`
**Repository**: `HouseholdBudgetMate`

## Research Question

Can the current charts in the app be improved, and is there a useful new chart that should be added for data the app already has?

## Summary

The app currently has three real chart surfaces: two annual statistics charts and one monthly plan pie chart. The rest of the relevant screens rely mostly on tables and KPI cards, even though the backend already computes several time series that are natural chart candidates.

The biggest gap is not Chart.js capability. It is data presentation. `Home` renders `SavingsTimeline` as a table, while `Statistics` renders `AccountBalances`, `MonthlyFinance`, `DeviationAlertCandidates`, and `CategoryBreakdown` mostly as tables. The UI is informative, but it does not always make trends easy to scan.

The best new chart candidate is an account balance over time chart based on `AccountBalances` and `AccountBalanceMonths`. A monthly cash-flow chart based on `MonthlyFinance` is also strong, but account balances should come first because that section is currently the hardest to read quickly.

## Detailed Findings

### Existing Chart Surfaces

- `Statistics` has two charts:
- `Monthly expenses` is a `mixed` bar and line chart where bars represent spent money and the line represents planned money ([Statistics.razor:522](../../../src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor#L522)).
- `Expense trend by category` is a line chart for selected categories ([Statistics.razor:538](../../../src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor#L538)).
- `PlanPage` has one pie chart for current-month expenses by category ([PlanPage.razor:1147](../../../src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor#L1147)).
- The shared chart component is `ChartCanvas`, backed by Chart.js through `charts.js`; it already supports `bar`, `line`, `pie`, and `mixed` chart types ([ChartCanvas.razor:1](../../../src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor#L1), [charts.js:1](../../../src/HouseholdBudgetMate.Web/wwwroot/js/charts.js#L1)).

### Table-Heavy Areas

- `Home` displays `SavingsTimeline` only as a table, even though it is a natural line or combo chart candidate ([Home.razor:257](../../../src/HouseholdBudgetMate.Web/Components/Pages/Home.razor#L257)).
- `Statistics` displays `AccountBalances` as a wide month-by-account table, which is hard to scan visually ([Statistics.razor:458](../../../src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor#L458)).
- The same page also displays `MonthlyFinance`, `DeviationAlertCandidates`, and `CategoryBreakdown` as tables, despite the backend already exposing chart-ready time series ([Statistics.razor:432](../../../src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor#L432), [Statistics.razor:367](../../../src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor#L367), [Statistics.razor:401](../../../src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor#L401)).

### Existing Data Supports Better Charts

- `YearStatisticsDto` already includes `MonthlyFinance`, `AccountBalances`, `CategoryBreakdown`, `DeviationAlertCandidates`, `TopCategories`, and `AccountBalanceMonths`, so a new chart does not need a new domain model first ([YearStatisticsDto.cs:3](../../../src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/YearStatisticsDto.cs#L3)).
- `MonthlyFinance` includes `IncomeAmount`, `PlannedAmount`, `SpentAmount`, `UnplannedSpentAmount`, `SavingsTransferredAmount`, and `SavedAmount`, which is enough for a readable finance chart ([YearMonthlyFinanceDto.cs:3](../../../src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/YearMonthlyFinanceDto.cs#L3)).
- `AccountYearBalanceDto` contains one monthly balance series per account, so it can directly drive a line chart without new aggregation work ([AccountYearBalanceDto.cs:3](../../../src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/AccountYearBalanceDto.cs#L3)).
- `DashboardSummaryDto` exposes `SavingsTimeline` and `CategoryRemainingItems`, which are currently table-first or not charted at all ([DashboardSummaryDto.cs:3](../../../src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/DashboardSummaryDto.cs#L3)).

### Backend Aggregations Are Already Available

- `GetYearStatisticsAsync` builds monthly cash flow, categories, tags, account balances, and alert candidates in one service projection, so the recommended charts are mostly UI work ([ExpenseService.cs:605](../../../src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L605)).
- `MonthlyFinance` is already projected as one row per populated month, while `AccountBalances` is already projected as one monthly series per account ([ExpenseService.cs:902](../../../src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L902)).
- `TopCategories` is computed but does not currently drive a dedicated chart on the statistics page. It is a good candidate for a small top-categories chart ([ExpenseService.cs:687](../../../src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L687)).

## Recommendations

### 1. Add An Account Balance Over Time Chart First

This is the strongest new chart candidate.

- It uses existing `AccountBalances` and `AccountBalanceMonths` data.
- It improves the least scannable section of the current statistics page.
- It shows trend and divergence between accounts, not just static table cells.

Suggested shape:

- A line chart for the most relevant accounts.
- Optional filtering for active accounts or top accounts.
- Tooltips with account name and closing balance.

### 2. Add A Monthly Cash-Flow Chart Next

This is the next best option if the goal is to make the yearly view easier to understand.

- Source data: `MonthlyFinance`.
- Useful series: `IncomeAmount`, `SpentAmount`, `SavingsTransferredAmount`, and `SavedAmount`.
- This chart answers whether each month was net positive and what drove the result.

### 3. Reduce Noise In The Existing Category Trend Chart

The current category line chart can become visually overloaded because it can render every selected category.

- Show the top 5-8 categories by total spending by default.
- Aggregate the rest into `Other` if needed.
- Keep the category selector as an override for users who want a detailed comparison.

### 4. Consider A Small Home Chart Later

`Home` could use a compact line chart for `SavingsTimeline`.

- It would give the dashboard a fast year-to-date savings trend.
- It should stay secondary because the home page is already KPI-heavy.
- The statistics page has the clearer first opportunity.

## Best Chart Candidate

If only one chart is added now, choose:

**Account balance over time on the `Statistics` page.**

Reasons:

- The data already exists.
- The current table is hard to read quickly.
- A line chart makes balance trends, drops, and account divergence much easier to see.
- No new domain model is required.

## Code References

- `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:522` - current statistics charts.
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:1147` - monthly plan pie chart.
- `src/HouseholdBudgetMate.Web/Components/Pages/Home.razor:257` - `SavingsTimeline` table, a possible mini-chart candidate.
- `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:458` - account balance table.
- `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:432` - monthly finance table.
- `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:1` - shared chart component.
- `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:1` - Chart.js integration and chart type handling.
- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:605` - annual statistics projection.
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/YearStatisticsDto.cs:3` - DTO with chart-ready yearly data.
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/YearMonthlyFinanceDto.cs:3` - monthly cash-flow DTO.
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/AccountYearBalanceDto.cs:3` - monthly account balance DTO.

## Architecture Insights

- The chart layer is already well separated: Razor builds datasets, `ChartCanvas` passes them to JS interop, and `charts.js` renders or updates Chart.js instances.
- New charts can reuse the current `ChartDataset` model as long as they fit `bar`, `line`, `pie`, or `mixed`.
- The main opportunity is not a new charting library. It is moving high-value time series out of wide tables and into focused visual summaries.

## Historical Context

- `context/archive/2026-06-03-improve-monthly-planning/research.md` previously identified `Statistics` as the home for annual aggregates and historical context.
- `context/foundation/roadmap.md:115` through `context/foundation/roadmap.md:135` shows that the most recent larger work focused on monthly planning and yearly income/savings context, so chart work should extend that area rather than open a separate product thread.

## Open Questions

- Should the next chart improve `Statistics` first, or should the dashboard on `Home` be simplified first?
- Should the account balance chart show all accounts, active accounts only, or the top accounts by absolute balance?
- Should the category trend chart remain a line chart, or should it move toward a top-categories plus `Other` view?
