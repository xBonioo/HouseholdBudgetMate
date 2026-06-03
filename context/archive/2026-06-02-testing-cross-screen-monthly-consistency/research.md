---
date: 2026-06-02T00:00:00+02:00
researcher: Codex
git_commit: c3f2b9e140ddd9b718f089d68c1a056e55e73856
branch: main
repository: HouseholdBudgetMate
topic: "Rollout Phase 1: cross-screen monthly consistency"
tags: [research, codebase, monthly-loop, ui-contract, tests]
status: complete
last_updated: 2026-06-02
last_updated_by: Codex
---

# Research: Rollout Phase 1: cross-screen monthly consistency

**Date**: 2026-06-02T00:00:00+02:00
**Researcher**: Codex
**Git Commit**: c3f2b9e140ddd9b718f089d68c1a056e55e73856
**Branch**: main
**Repository**: HouseholdBudgetMate

## Research Question

Ground rollout Phase 1 of `context/foundation/test-plan.md`: "Cross-screen monthly consistency".

Verify risks #1 and #4:

- #1: Cross-screen monthly state diverges after edits, so Plan, Accounts, Dashboard/Home, or Statistics tell different budget stories.
- #4: Monthly financial contract regresses toward stale Safe-to-spend wording or incomplete-balance behavior.

The plan's response intent is to prove one edited monthly scenario produces consistent user-visible state across key monthly screens, while old Safe-to-spend labels stay absent and incomplete balance remains explicit.

## Summary

The cheapest useful protection is not browser e2e by default. The app is reload-based: Plan mutates persisted month state and reloads its projections; Home, Accounts, and Statistics read their own projections when loaded or reloaded. There is no cross-screen live notification path, so the concrete regression to catch is "after a monthly edit and reload, all screen backing projections agree."

Current coverage is strong for the accepted S-02 monthly loop but incomplete for Phase 1. `MonthlyBudgetingLoopTests` already proves the service-level controlled scenario through planned expense, actual spend, unexpected expense, savings timing, close/reopen/edit/close, and final values. It only reads `ExpenseService.GetMonthAsync` and `IncomeService.GetLiveBalanceAsync`, so it does not prove Dashboard/Home or Statistics projections agree after edits.

The best next plan is:

1. Extend service integration coverage to assert a deterministic edited scenario across `GetMonthAsync`, `GetLiveBalanceAsync`, `GetDashboardSummaryAsync`, and `GetYearStatisticsAsync`.
2. Tighten the existing static UI contract tests to assert each screen's actual role: Plan/Home show `Pozostało w planie` and `Live balance`; Accounts shows `Live balance` and account/savings context, not monthly plan remaining; Statistics is annual/monthly finance context, not a live-balance screen.
3. Keep Safe-to-spend absence and incomplete-balance guidance as explicit UI contract checks.

## Detailed Findings

### Source Of Truth And Mutation Flow

Monthly state is persisted in domain rows, not cached in UI state. `MonthPlan` owns year/month, closed state, expenses, and savings transfers (`src/HouseholdBudgetMate.Domain/Entities/MonthPlan.cs:5`). Expenses carry planned amount, actual amount, and whether remaining value is shown (`src/HouseholdBudgetMate.Domain/Entities/Expense.cs:5`). Savings transfers are dated month-plan children (`src/HouseholdBudgetMate.Domain/Entities/MonthSavingsTransferItem.cs:5`). Live balance starts from account closing balances (`src/HouseholdBudgetMate.Domain/Entities/AccountMonthBalance.cs:5`) plus dated incomes (`src/HouseholdBudgetMate.Domain/Entities/Income.cs:5`).

`ExpenseService.GetMonthAsync` is the month-plan projection used by Plan. It creates/syncs a month when missing, loads expenses and savings transfers, and builds `MonthPlanDto` (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:326`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:343`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:358`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2019`).

Plan remaining is derived on read from expense DTOs. The KPI calculation sums visible remaining amounts, treats hidden untouched planned rows specially, excludes unplanned rows, and returns `MonthPlanKpiDto` (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2036`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2059`). The DTO shape is only planned, spent, remaining, and percent (`src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/MonthPlanKpiDto.cs:3`).

`IncomeService.GetLiveBalanceAsync` is the live-balance projection. It computes previous non-savings account balances, due incomes, actual expenses, and due savings transfers, then returns `CurrentBalance = accountBaseTotal + incomesTotal - expensesTotal - savingsTransfersTotal` (`src/HouseholdBudgetMate.Application/Services/IncomeService.cs:399`, `src/HouseholdBudgetMate.Application/Services/IncomeService.cs:492`). `LiveBalanceDto` carries those components plus balance-base completeness and missing-account names (`src/HouseholdBudgetMate.Abstractions/Contracts/Incomes/Dto/LiveBalanceDto.cs:3`).

Plan mutation handlers call services and then reload Plan state. Expense create/update/delete call `LoadAsync()` after the service operation (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:80`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:167`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:195`). Income, line-item, savings-transfer, and close/open handlers do the same (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Incomes.cs:110`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:70`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.SavingsTransfers.cs:33`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:208`).

There is no evidence of pub/sub synchronization between already-open screens. Home and Accounts read the same service projections on page load or selected-period reload (`src/HouseholdBudgetMate.Web/Components/Pages/Home.razor:359`, `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs:124`, `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs:241`). Statistics reads the annual projection path through `GetYearStatisticsAsync` (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:543`).

### Screen Contracts

Plan is the strongest monthly contract surface. It shows `Pozostało w planie` from `_kpi.RemainingTotal` and `_kpi.RemainingPercent` (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:117`), shows `Live balance` gated by `_liveBalance.HasCompleteBalanceBase` (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:128`), and renders incomplete balance as warning guidance (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:107`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.cs:178`). It also shows monthly savings context (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:146`).

Dashboard/Home carries the same high-level contract for the current month. It shows `Pozostało w planie` (`src/HouseholdBudgetMate.Web/Components/Pages/Home.razor:103`), savings KPIs (`src/HouseholdBudgetMate.Web/Components/Pages/Home.razor:126`), `Płynność miesiąca` / `Live balance` (`src/HouseholdBudgetMate.Web/Components/Pages/Home.razor:159`), and incomplete-balance guidance from the same wording pattern (`src/HouseholdBudgetMate.Web/Components/Pages/Home.razor:333`).

Accounts carries liquidity, account, envelope, and savings context for the selected month, but not monthly `Pozostało w planie`. It renders `Live balance` (`src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor:93`) and incomplete guidance backed by `_overview.MissingBalanceAccountNames` (`src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor:84`, `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs:76`). The `Pozostało` text there is envelope-specific, `Pozostało z limitu koperty`, not the monthly plan KPI (`src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor:191`).

Statistics is annual/monthly finance context, not a live-balance screen. It loads `ExpenseService.GetYearStatisticsAsync` (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:543`), shows `Podsumowanie miesięczne (wpływy, plan, oszczędności)` (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:339`), and derives year-summary ranges from `MonthlyFinance` (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:686`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:696`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:722`). Research found no `Live balance` or incomplete-balance guidance in Statistics, so Phase 1 should not force that label there.

### Existing Test Coverage

`MonthlyBudgetingLoopTests` already covers the accepted S-02 controlled scenario: initial live balance, planned expense, actual spend, unexpected spend, future/due savings transfer, close/reopen/edit/close, and closed-month edit blocking (`src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:23`). The final state asserts `7075` live balance and `800` plan remaining (`src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:141`). Its helper reads only `ExpenseService.GetMonthAsync` and `IncomeService.GetLiveBalanceAsync` (`src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:266`).

`IncomeServiceTests` has broad live-balance coverage: formula, due/future savings transfers, incomplete base balances, stored zero balance, archive/activation cases, unplanned expenses, and no-month-plan behavior (`src/HouseholdBudgetMate.Tests/Tests/Services/IncomeServiceTests.cs:1056`, `src/HouseholdBudgetMate.Tests/Tests/Services/IncomeServiceTests.cs:1177`, `src/HouseholdBudgetMate.Tests/Tests/Services/IncomeServiceTests.cs:1239`, `src/HouseholdBudgetMate.Tests/Tests/Services/IncomeServiceTests.cs:1339`, `src/HouseholdBudgetMate.Tests/Tests/Services/IncomeServiceTests.cs:1666`).

`ExpenseServiceTests` covers month-plan KPI rules, savings transfer CRUD, dashboard summary, year statistics, line items, and close/open behavior (`src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:456`, `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:424`, `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:1164`, `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:1838`, `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:2318`).

`MonthlyBudgetingLoopUiTests` are static source/text contract tests, not rendered component or browser tests. They assert accepted labels, service calls, and absence of stale `Safe-to-spend`/`SafeToSpend` wording for Plan, Home, Accounts, and Statistics (`src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:8`, `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:32`, `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:56`, `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:67`).

The test project has xUnit, FluentAssertions, Moq, NetArchTest, coverlet, EF InMemory, and EF SQLite references (`src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj:12`). It references the Web project but has no bUnit, Playwright, or ASP.NET integration-test package. Existing EF tests use InMemory; SQLite is referenced but no `UseSqlite` usage was found.

### Safe-to-spend And Incomplete Balance

The active product contract excludes separate Safe-to-spend. The roadmap says F-01 is superseded and the MVP uses `Live balance`, `Pozostało w planie`, savings context, and month lifecycle (`context/foundation/roadmap.md:63`, `context/foundation/roadmap.md:102`). The accepted evidence says no separate safe-to-spend field, reserve field, or UI KPI is part of acceptance (`context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md:33`).

Active app UI surfaces do not contain `Safe-to-spend` or `SafeToSpend`; the active hits are the guard tests and context docs. The UI guard tests explicitly assert absence in Plan/Home/Accounts surfaces (`src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:27`, `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:51`).

Incomplete-balance guidance should remain explicit. Plan and Home build the same guidance around missing previous-month closing balances (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.cs:178`, `src/HouseholdBudgetMate.Web/Components/Pages/Home.razor:341`), and Accounts mirrors that wording through its overview model (`src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs:84`).

## Code References

- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:326` - `GetMonthAsync` starts the month projection path.
- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2036` - `CalculateMonthPlanKpi` derives plan remaining from expense DTOs.
- `src/HouseholdBudgetMate.Application/Services/IncomeService.cs:399` - `GetLiveBalanceAsync` starts live-balance calculation.
- `src/HouseholdBudgetMate.Application/Services/IncomeService.cs:492` - live-balance formula and DTO creation.
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:24` - Plan reloads month, dashboard summary, incomes, and live balance together.
- `src/HouseholdBudgetMate.Web/Components/Pages/Home.razor:359` - Home reads live balance on load.
- `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs:124` - Accounts reads live balance for selected period.
- `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:543` - Statistics reads annual/monthly finance through `GetYearStatisticsAsync`.
- `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:23` - existing controlled monthly-loop service scenario.
- `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:8` - existing static UI contract guard for Plan.

## Architecture Insights

The architecture already has a useful separation for Phase 1. Service projections are the cheapest reliable oracle for cross-screen consistency because screens mostly format and display service DTOs. The test should therefore avoid copying production calculations into expected values; expected values should come from the accepted scenario in `acceptance-evidence.md`.

Plan/Home/Accounts/Statistics are not identical screens. A good test does not force equal labels everywhere. It should assert consistent underlying month truth and screen-appropriate presentation:

- Plan and Home: monthly plan remaining, live balance, savings context, incomplete-balance guidance.
- Accounts: live balance and account/savings/envelope context; no monthly plan KPI label.
- Statistics: annual/monthly finance rollups from `GetYearStatisticsAsync`; no live-balance requirement unless product scope changes.

The most important anti-pattern to avoid is an implementation mirror: computing expected values in the test by applying the same formulas as `ExpenseService` and `IncomeService`. Use the independent controlled scenario table from `acceptance-evidence.md` as the oracle.

## Historical Context

`context/foundation/roadmap.md:26` defines S-02 as the current north-star MVP: PIN, monthly plan, actual/unexpected expenses, `Live balance`, `Pozostało w planie`, savings, and close/reopen/edit/close. `context/foundation/roadmap.md:63` says the historical Safe-to-spend contract is superseded.

`context/changes/align-safe-to-spend-contract/plan.md:3` is historical and explicitly superseded. It remains useful evidence for why incomplete prior-month balances and cross-screen label drift matter.

`context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md:28` records the accepted S-02 financial model. Its controlled scenario table gives independent expected values for the test oracle (`context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md:51`).

`context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md:82` says the current UI evidence is a lightweight xUnit component/UI contract harness, not a full browser-clicking test.

## Related Research

No prior `research.md` artifact was found for this change. Relevant prior planning/evidence artifacts:

- `context/changes/verify-monthly-safe-to-spend-loop/plan.md`
- `context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md`
- `context/changes/align-safe-to-spend-contract/plan.md`
- `context/foundation/test-plan.md`

## Test Plan Corrections To Consider

Research does not require an immediate test-plan rewrite, but planning should account for these corrections:

- Phase 1 is an extension of existing S-02 service/static UI coverage, not a new gate from zero.
- `browser/e2e | none yet` is accurate for full browser/e2e, but the stack notes should also acknowledge the existing xUnit static UI contract baseline.
- Risk #1 guidance should say prior S-02 proves controlled Plan/Home/Accounts semantics, but does not yet prove edit-driven agreement across Home/Accounts/Statistics projections.
- Statistics should be treated as annual/monthly finance context, not a screen that must display `Live balance`.

## Open Questions

- Should Phase 1 use the existing EF InMemory fixture for lowest cost, or introduce SQLite for stronger EF query/relational behavior signal? Research recommends InMemory first unless planning decides relational uniqueness/query translation is part of this risk.
- Should Phase 1 include a true rendered component or browser test? Research recommends no unless the planned regression is already-open-screen staleness. The current app appears reload-based, so service projection plus static UI contract gives better cost x signal.
