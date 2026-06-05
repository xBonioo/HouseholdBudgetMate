---
date: 2026-06-03T10:12:46+02:00
researcher: Codex
git_commit: ad74069dbd9b691869043b4d9b1876f72669dce5
branch: main
repository: HouseholdBudgetMate
topic: "Improve monthly planning"
tags: [research, codebase, monthly-planning, plan-page, recurring-expenses, statistics]
status: complete
last_updated: 2026-06-03
last_updated_by: Codex
---

# Research: Improve monthly planning

**Date**: 2026-06-03T10:12:46+02:00
**Researcher**: Codex
**Git Commit**: ad74069dbd9b691869043b4d9b1876f72669dce5
**Branch**: main
**Repository**: HouseholdBudgetMate

## Research Question

Research the `improve-monthly-planning` change so the next `/10x-plan` can improve monthly planning with copies, history, recurring expenses, yearly suggestions, alert preparation, and annual income/savings planning.

## Summary

S-03 is a ready roadmap slice that builds on the completed S-02 monthly loop. The canonical outcome is faster month preparation through copy, history, active recurring expenses, suggestion rules, alert-prep, and annual income/savings context (`context/foundation/roadmap.md:115`).

The current app already has useful scaffolding:

- PlanPage supports selecting existing expenses and copying them, but only to the next calendar month (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:474`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:273`).
- The backend copy path preserves name/category/tag/planned amount/show-remaining, resets actual amount to zero, and skips duplicate recurring definitions in the target month (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1609`).
- Active recurring expenses are already synced idempotently into newly created month plans (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2359`), backed by a unique `(MonthPlanId, RegularExpenseDefinitionId)` index (`src/HouseholdBudgetMate.Domain/EntityConfiguration/ExpenseConfiguration.cs:67`).
- Statistics already exposes historical search, category annual averages, category-by-month breakdown, and monthly finance rollups (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:36`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:307`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:338`).

Main design tensions for planning:

- The roadmap says suggestions must be user-approved proposals, not silent automatic plan generation (`context/foundation/roadmap.md:135`), but `GetMonthAsync` currently auto-syncs recurring expenses/incomes/loan installments when a month plan is created (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:332`).
- Annual statistics include only months with actual spending, so planned-only future months may not appear in the existing Statistics annual views (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:581`).
- There is no existing annual-plan persistence model or `Plan roczny` UI; Statistics is currently read-only actuals/rollups, not editable planning (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:284`).
- Do not revive the superseded separate `Safe-to-spend` output. Current acceptance is `Live balance`, `Pozostalo w planie`, savings context, and incomplete-balance guidance (`context/foundation/prd.md:105`).

## Detailed Findings

### Product Oracle

The roadmap defines S-03 as "Usprawnienia planowania miesiecy": the user should prepare a month faster using copies, historical suggestions, active recurring expenses, alert foundations, and annual income/savings context (`context/foundation/roadmap.md:115`, `context/foundation/roadmap.md:117`).

Scope notes are explicit:

- Finish or verify PlanPage `_isCopyMode` so a month can be copied into another month, with the example July 2024 to July 2025 (`context/foundation/roadmap.md:128`).
- When creating a new plan, ask which same-month-previous-year items to copy in addition to active recurring items; use name similarity and avoid obvious duplicates (`context/foundation/roadmap.md:129`).
- Suggest amounts from spent amount, a buffer, and rounding to tens or hundreds based on scale (`context/foundation/roadmap.md:130`).
- Suggest plans from the last 3 months' historical category averages (`context/foundation/roadmap.md:131`).
- Prepare deviation alerts when a category exceeds historical average by more than 20%, without sending real notifications in this slice (`context/foundation/roadmap.md:132`).
- Automatically suggest active recurring expenses when they have not already been added (`context/foundation/roadmap.md:133`).
- Add the ability to plan expected annual income and savings in a Statistics `Plan roczny` section (`context/foundation/roadmap.md:134`).

The PRD frames recurring/reusable expenses and generated next-month plans as later-iteration nice-to-haves, with a guardrail that automatic month preparation must not duplicate recurring items (`context/foundation/prd.md:38`, `context/foundation/prd.md:43`, `context/foundation/prd.md:71`, `context/foundation/prd.md:73`).

### PlanPage UI And State

PlanPage is route-bound to `/plan/{Year:int}/{Month:int}` and loads month plan, dashboard summary, incomes, live balance, categories, accounts, and tag usage together (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:1`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:24`).

Closed-month state is visible in the header and most mutation handlers call `EnsureMonthEditable()`, which warns and returns false for closed months (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:39`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:231`). Copy mode does not call `EnsureMonthEditable()` locally; the backend enforces the target month being open, but source-closed behavior should be intentional in the plan (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:265`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1634`).

Month navigation is previous/current/next routing and clears edit/copy state before navigation (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:146`). This is enough for adjacent-month copy, but not for roadmap copy from arbitrary historical months or same-month-previous-year source/target selection.

Copy-mode scaffolding exists:

- `_isCopyMode` and `_selectedExpenseIdsForCopy` live in PlanPage state (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.cs:34`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.cs:138`).
- Header buttons switch copy mode and submit selected rows (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:474`).
- Row checkboxes appear in the order column while editing/deleting/line-item actions are disabled (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:643`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:682`).
- `CopySelectedExpensesAsync` confirms, preserves selected row order, calls `CopySelectedExpensesToNextMonthAsync`, clears copy state, and reports copied count (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:265`).

PlanPage has a recurring management link, but no local suggestions or recommendation panel (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:264`). Adding suggestions should account for dirty-state tracking, which currently snapshots savings, income, expense, and line-item forms (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.cs:214`).

### Monthly Services And Persistence

`MonthPlan` is the central persistence object: user/year/month/closed state with child expenses and savings transfers (`src/HouseholdBudgetMate.Domain/Entities/MonthPlan.cs:5`). Month plans are unique per user/year/month (`src/HouseholdBudgetMate.Domain/EntityConfiguration/MonthPlanConfiguration.cs:30`).

Available/archive months are simply existing `MonthPlans`, sorted descending (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:45`). Since many flows use get-or-create month behavior, visiting or preparing future months can make them appear in archive/month lists (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1992`).

`GetMonthAsync` uses `GetOrCreateMonthPlanStateAsync`; when a plan was newly created and open, it syncs regular expenses, regular incomes, and loan installments (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:326`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:332`). `CloseMonthAsync` creates/syncs the next month only if the next month did not already exist (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:201`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:216`). `OpenMonthAsync` does not backfill sync into an already existing month (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:232`).

Regular expenses are active/inactive definitions with name, category, tag, amount, order, and show-remaining flag (`src/HouseholdBudgetMate.Domain/Entities/RegularExpenseDefinition.cs:5`). Sync is idempotent: it skips existing `RegularExpenseDefinitionId`s and uses current definition order (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2359`). Persistence also prevents duplicates through a unique `(MonthPlanId, RegularExpenseDefinitionId)` index (`src/HouseholdBudgetMate.Domain/EntityConfiguration/ExpenseConfiguration.cs:67`).

The existing copy service copies selected expenses only to the next month (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1609`). It:

- validates selected IDs are present in the source month (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1619`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1626`);
- creates or loads the next month and requires it open (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1631`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1634`);
- skips recurring expenses whose definition already exists in the target (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1636`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1655`);
- appends copied rows after existing target expenses (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1647`);
- preserves planned attributes and resets actual amount to zero (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1662`).

It does not copy expense line items. That is likely correct for planning because actual details belong to the source month, but the plan should state this explicitly.

### History, Suggestions, And Statistics

`SearchExpenseHistoryAsync` already supplies a historical search foundation. It filters expenses by month range/category/query/root tag/subtag and returns expense name, category, tag hierarchy, planned amount, actual amount, and matching line-item description (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:936`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:957`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1053`). Statistics calls this from the "Wyszukiwarka zakupow" form (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:36`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:737`).

`GetYearStatisticsAsync` exposes useful data for historical averages: category totals/average monthly spent, tag statistics, category-by-month breakdown, monthly finance, and account balances (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:549`, `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/YearStatisticsDto.cs:3`). Category averages are based on `populatedMonths`, not all 12 months (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:581`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:602`).

`MonthlyFinance` includes income amount, planned amount, spent amount, unplanned spent amount, savings transferred, and saved amount (`src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/YearMonthlyFinanceDto.cs:3`). It is built only for populated months, so a future annual plan or planned-only month may need a new projection or a change in definition (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:843`).

Statistics currently has annual summary, category history, and monthly finance sections, but no editable `Plan roczny` surface (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:284`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:307`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:338`). A `Plan roczny` feature therefore needs new persistence/contracts, unless planning remains transient and derived.

### Live Balance, Savings, And No Safe-To-Spend

The active product contract excludes a separate `Safe-to-spend` amount. The PRD says MVP output is `Live balance`, `Pozostalo w planie`, savings context, and incomplete-balance guidance (`context/foundation/prd.md:105`). The archived S-02 evidence records the user decision that no separate safe-to-spend field, reserve field, or UI KPI is part of acceptance (`context/archive/2026-05-29-verify-monthly-safe-to-spend-loop/acceptance-evidence.md:24`, `context/archive/2026-05-29-verify-monthly-safe-to-spend-loop/acceptance-evidence.md:33`).

Live balance remains separate from plan suggestions. `IncomeService.GetLiveBalanceAsync` uses previous non-savings account balances, due incomes, actual expenses, and due savings transfers (`src/HouseholdBudgetMate.Application/Services/IncomeService.cs:399`) and returns `CurrentBalance = accountBaseTotal + incomesTotal - expensesTotal - savingsTransfersTotal` (`src/HouseholdBudgetMate.Application/Services/IncomeService.cs:492`).

Savings transfers are month-plan children with amount and transfer date (`src/HouseholdBudgetMate.Domain/Entities/MonthSavingsTransferItem.cs:5`). Live balance date-gates transfers, while annual statistics sum all transfers in the month (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:793`). Annual savings planning should not blur actual transferred savings with expected annual savings goals unless the plan introduces a distinct model.

### Tests And Quality Signals

The cheapest useful verification layer should remain service/contract tests first. The test plan says service projection integration is the primary numeric guard for monthly edits, and UI contract tests guard labels, service wiring, incomplete-balance guidance, and absence of stale Safe-to-spend wording (`context/foundation/test-plan.md:96`, `context/foundation/test-plan.md:97`).

Existing tests to extend:

- Copy behavior: `ExpenseServiceTests.CopySelectedExpensesToNextMonthAsync_Should_Copy_Selected_Items_With_Actual_Set_To_Zero` (`src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:614`).
- Regular expense creation/sync/idempotency: `ExpenseServiceTests` around regular expense definitions and add-to-month (`src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:709`, `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:1041`).
- History search: `ExpenseServiceTests` history-search coverage starts around `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:1695`.
- Annual statistics: `GetYearStatisticsAsync` tests cover category metrics, populated months, account applicability, line-item aggregation, and empty-year behavior (`src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:1164`, `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:1519`, `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:2849`).
- Monthly loop consistency: `MonthlyBudgetingLoopTests` already reads month, live balance, dashboard, and statistics together (`src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:25`, `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:357`).
- Static UI contract: `MonthlyBudgetingLoopUiTests` guards Plan/Home/Accounts/Statistics wording and service wiring (`src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:13`).

## Code References

- `context/foundation/roadmap.md:115` - S-03 scope, outcome, open questions, scope notes, and risk.
- `context/foundation/prd.md:38` - automatic month preparation is a later iteration.
- `context/foundation/prd.md:43` - automatic preparation must not duplicate recurring items.
- `context/foundation/prd.md:105` - current no-Safe-to-spend business output.
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:474` - copy-mode header controls.
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:265` - selected-copy UI handler.
- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1609` - backend copy-selected-expenses implementation.
- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2359` - recurring expense sync implementation.
- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:936` - expense history search implementation.
- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:549` - annual statistics projection.
- `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:338` - monthly finance table in Statistics.
- `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:614` - existing copy test.
- `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:25` - existing cross-screen monthly-loop integration test.
- `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:13` - existing static UI contract guard.

GitHub permalink base for this research snapshot:
`https://github.com/xBonioo/HouseholdBudgetMate/blob/ad74069dbd9b691869043b4d9b1876f72669dce5/`

## Architecture Insights

The app already separates "plan state" from "financial projections" well enough for S-03:

- Month preparation should operate on month plan expenses, recurring definitions, incomes, and savings transfers.
- Trustworthy current-month state should remain the S-02 projection contract: month KPI, dashboard summary, live balance, and statistics.
- Suggestions should probably be explicit DTOs and user-approved commands, not side effects hidden in `GetMonthAsync`.

The largest architectural decision is whether to keep auto-sync-on-create for recurring items or reframe it as a suggestion source for new plans. The roadmap risk pushes toward visible proposals, but existing code already silently creates active recurring expenses when the month plan is first created. A plan should either preserve this as accepted current behavior and add user-approved historical suggestions around it, or deliberately phase a behavior change with regression tests.

Annual planning is not just a UI addition. Existing annual statistics are actuals-focused and skip planned-only months. Supporting expected annual income/savings likely needs a new domain model and service contract, or a clearly transient calculation if persistence is intentionally deferred.

## Historical Context

- `context/archive/2026-05-29-verify-monthly-safe-to-spend-loop/plan.md` - S-02 accepted the monthly loop without adding a separate Safe-to-spend amount; the current contract is `Live balance`, `Pozostalo w planie`, savings timing, incomplete-balance guidance, and month lifecycle.
- `context/archive/2026-05-29-verify-monthly-safe-to-spend-loop/acceptance-evidence.md` - records the 2026-05-29/2026-05-30 user decision not to include a separate safe-to-spend field, reserve field, or UI KPI.
- `context/archive/2026-06-02-testing-cross-screen-monthly-consistency/research.md` - established that service projection integration plus static/rendered UI contracts are the cheapest useful monthly-loop protection, not broad browser e2e.
- `context/archive/2026-05-26-align-safe-to-spend-contract/plan.md` - historical and superseded; useful only as a caution that old safe-to-spend language must not leak back into S-03.

## Related Research

- `context/archive/2026-06-02-testing-cross-screen-monthly-consistency/research.md`
- `context/archive/2026-05-29-verify-monthly-safe-to-spend-loop/plan.md`
- `context/archive/2026-05-29-verify-monthly-safe-to-spend-loop/acceptance-evidence.md`

## Open Questions

- Should S-03 change existing auto-sync-on-new-month behavior into visible recurring-item suggestions, or preserve auto-sync and only make historical/yearly suggestions approval-based?
- What default exclusion rule should deviation alerts use for intentionally irregular categories such as construction or other one-off categories?
- What exact rounding rule should be used for suggested amounts after buffer, especially at small values?
- Should annual income/savings planning persist a user-authored annual plan, or should it be calculated from monthly income and savings-transfer plans?
- Should arbitrary-month copy include only selected expense plan rows, or also optional incomes and savings transfers?
