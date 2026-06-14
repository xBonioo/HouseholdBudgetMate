---
date: 2026-06-14T19:14:43+02:00
researcher: Codex
git_commit: 58e67dc20883abb3278765afd6b6124fca2f0b2e
branch: main
repository: HouseholdBudgetMate
topic: "Domain refactor based on context/domain/01-domain-distillation.md"
tags: [research, codebase, domain, monthly-reconciliation, month-plan, live-balance, expenses, account-balances]
status: complete
last_updated: 2026-06-14
last_updated_by: Codex
---

# Research: Domain refactor based on context/domain/01-domain-distillation.md

**Date**: 2026-06-14T19:14:43+02:00
**Researcher**: Codex
**Git Commit**: 58e67dc20883abb3278765afd6b6124fca2f0b2e
**Branch**: main
**Repository**: HouseholdBudgetMate

## Research Question

Prepare codebase research for `domain-refactor`: repair the domain layer and shape refactor proposals from `context/domain/01-domain-distillation.md`, with primary focus on `MonthlyFinancialPicture` / `MonthPlan` as the monthly reconciliation boundary.

## Summary

The distillation is directionally right: the core domain problem is not a missing CRUD feature, but an unnamed monthly reconciliation boundary. Today the accepted monthly picture is split across `ExpenseService` (`MonthPlan`, plan KPI, savings-transfer CRUD), `IncomeService` (`Live balance`), `AccountService` (closing balances), Blazor page partials, and service/UI contract tests.

The best first refactor target is a small application/domain projection boundary for the monthly financial picture, not a broad rewrite of persistence entities. Current architecture explicitly treats domain entities as persistence-oriented and keeps workflow in application services, so the safest plan is staged: first name and centralize calculations and invariants, then consider whether entities should become more encapsulated.

Research also corrects one important assumption in the distillation: `AccountMonthBalance` uniqueness and recurring generated-row uniqueness are already protected by EF unique indexes. The risk there is not absence of DB constraints; it is that the concepts are not surfaced as named domain/application policies.

## Detailed Findings

### Monthly Reconciliation Boundary

- `MonthPlan` is a persistence entity with public setters for year, month, closed state, expenses, and savings transfers. It does not enforce lifecycle rules itself ([MonthPlan.cs:5](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Domain/Entities/MonthPlan.cs#L5)).
- `ExpenseService.GetMonthAsync` creates or loads the month, syncs regular expenses/incomes/loan installments for a newly created open month, loads expenses and savings transfers, then builds `MonthPlanDto` ([ExpenseService.cs:382](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L382)).
- `CloseMonthAsync` and `OpenMonthAsync` also own month lifecycle and next-month preparation, including recurring expenses, recurring incomes, and loan installment sync ([ExpenseService.cs:257](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L257), [ExpenseService.cs:288](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L288)).
- `Pozostalo w planie` is calculated from expense DTOs only; savings transfers are not part of that KPI ([ExpenseService.cs:2492](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L2492), [ExpenseService.cs:2509](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L2509)).
- `Live balance` is calculated separately in `IncomeService`: previous non-savings account base plus due incomes minus actual expenses minus due savings transfers ([IncomeService.cs:399](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/IncomeService.cs#L399), [IncomeService.cs:469](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/IncomeService.cs#L469), [IncomeService.cs:492](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/IncomeService.cs#L492)).
- Balance-base completeness is part of the live-balance projection, not a separate model: missing previous rows populate `MissingBalanceAccountNames`; stored zero is complete because the account id exists in the balance dictionary ([IncomeService.cs:416](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/IncomeService.cs#L416), [IncomeService.cs:457](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/IncomeService.cs#L457), [IncomeService.cs:501](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/IncomeService.cs#L501)).
- Closed historical months use the latest available historical account balance rather than requiring the immediately preceding month ([IncomeService.cs:441](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/IncomeService.cs#L441)).

### Closed-Month Read-Only Rules

- The core guard is `BudgetHelper.EnsureMonthIsOpen`, which throws when `MonthPlan.IsClosed` is true ([BudgetHelper.cs:14](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Helpers/BudgetHelper.cs#L14)).
- The guard is duplicated across service operations: savings transfers ([ExpenseService.cs:1645](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L1645)), expense update ([ExpenseService.cs:2241](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L2241)), line items ([ExpenseService.cs:2137](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L2137)), and account balance upsert ([AccountService.cs:182](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/AccountService.cs#L182)).
- UI also has separate guards through `EnsureMonthEditable` in page partials. The service guard is authoritative, but UI affordances are not consistently disabled for every savings-transfer action, so a future UI polish item should make closed state visually consistent.

### Expense and Line-Item Actual Amount

- `Expense.ActualAmount` is persisted on the parent expense, while line items store their own amounts ([Expense.cs:16](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Domain/Entities/Expense.cs#L16), [ExpenseLineItem.cs:5](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Domain/Entities/ExpenseLineItem.cs#L5)).
- Effective actual amount is explicitly defined in `ExpenseActualAmountCalculator`: sum line items when any exist, otherwise use persisted parent `ActualAmount` ([ExpenseActualAmountCalculator.cs:7](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Helpers/ExpenseActualAmountCalculator.cs#L7)).
- `GetMonthAsync` maps expenses through the effective calculator, so Plan page DTOs can be correct even if persisted parent actual drifts ([ExpenseExtensionMapping.cs:23](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs#L23)).
- Live balance does not use the effective calculator; it subtracts persisted `Expense.ActualAmount` ([IncomeService.cs:480](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/IncomeService.cs#L480)). Some dashboard/statistics paths also trust persisted actuals.
- Create/update/delete line-item operations recalculate parent actual after mutation ([ExpenseService.cs:2085](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L2085), [ExpenseService.cs:2176](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L2176), [ExpenseService.cs:2220](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L2220)).
- Recalculation intentionally no-ops when no line items remain, preserving the last calculated parent actual ([ExpenseService.cs:2734](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/ExpenseService.cs#L2734)). Tests document that behavior, so changing it requires an explicit product decision.
- Backup restore inserts expenses and line items independently; it does not visibly recalculate parent actual after restoring line items ([BackupRestoreExecutor.cs:316](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/Backup/BackupRestoreExecutor.cs#L316), [BackupRestoreExecutor.cs:334](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/Backup/BackupRestoreExecutor.cs#L334)).
- Line-item request validation checks id, description, and optional tag id, but not amount sign/range ([ExpenseRequestValidators.cs:272](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs#L272)). If negative line items are refund support, that should be named and tested; if not, this is a bug candidate.

### Account Balances and Missing-vs-Zero

- `AccountMonthBalance` is a simple row with `AccountId`, `Year`, `Month`, and required `ClosingBalance` ([AccountMonthBalance.cs:5](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Domain/Entities/AccountMonthBalance.cs#L5)).
- EF enforces one row per account-month through a unique index on `{ AccountId, Year, Month }` ([AccountMonthBalanceConfiguration.cs:30](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Domain/EntityConfiguration/AccountMonthBalanceConfiguration.cs#L30)).
- `AccountService.UpsertMonthBalanceAsync` checks month openness, verifies the account, then inserts or updates by account/year/month ([AccountService.cs:182](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/AccountService.cs#L182), [AccountService.cs:196](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Application/Services/AccountService.cs#L196)).
- Tests already cover missing previous balance as incomplete and stored zero as complete (`IncomeServiceTests.cs:1339`, `IncomeServiceTests.cs:1371` from subagent findings). The concept is well protected but not yet named as a first-class policy.

### Recurring Generation

- Duplicate prevention for generated regular expenses is both procedural and database-backed: `Expense.RegularExpenseDefinitionId` links generated rows to definitions, and EF has a unique `{ MonthPlanId, RegularExpenseDefinitionId }` index ([Expense.cs:14](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Domain/Entities/Expense.cs#L14), [ExpenseConfiguration.cs:67](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Domain/EntityConfiguration/ExpenseConfiguration.cs#L67)).
- Duplicate prevention for generated regular incomes is also database-backed through unique `{ Year, Month, RegularIncomeDefinitionId }` ([IncomeConfiguration.cs:56](https://github.com/xBonioo/HouseholdBudgetMate/blob/58e67dc20883abb3278765afd6b6124fca2f0b2e/src/HouseholdBudgetMate.Domain/EntityConfiguration/IncomeConfiguration.cs#L56)).
- Service sync paths use `IgnoreQueryFilters()` when checking existing generated rows, so soft-deleted generated rows still block duplicate regeneration. This is a meaningful domain decision to preserve or revisit explicitly during planning.

### Historical Product Contract

- The accepted model is not a separate `Safe-to-spend`. Prior work explicitly superseded that concept in favor of `Live balance`, plan remaining, savings context, and incomplete-balance guidance (`context/archive/2026-05-29-verify-monthly-safe-to-spend-loop/acceptance-evidence.md`).
- The canonical numeric oracle remains the S-02 monthly loop: final live balance `7075.00`, plan remaining `800.00`, due savings `300.00`, future savings `600.00` after close/reopen/edit/close (`context/archive/2026-05-29-verify-monthly-safe-to-spend-loop/acceptance-evidence.md`).
- Cross-screen consistency research says service projections are the oracle; Plan, Home, Accounts, and Statistics do not need identical labels everywhere (`context/archive/2026-06-02-testing-cross-screen-monthly-consistency/research.md`).
- Refactor-opportunities history warns that full `PlanPage.LoadAsync` reloads are load-bearing for post-mutation consistency and should not be removed as incidental cleanup (`context/archive/2026-06-12-refactor-opportunities/plan.md`).

## Code References

- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:257` - close month lifecycle and next-month sync.
- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:382` - monthly plan read model creation.
- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2492` - `MonthPlanDto` and KPI construction.
- `src/HouseholdBudgetMate.Application/Services/IncomeService.cs:399` - live-balance projection entry point.
- `src/HouseholdBudgetMate.Application/Services/IncomeService.cs:416` - previous balance base loading.
- `src/HouseholdBudgetMate.Application/Services/IncomeService.cs:480` - live balance subtracts persisted expense actuals.
- `src/HouseholdBudgetMate.Application/Services/AccountService.cs:182` - account-month balance upsert and open-month guard.
- `src/HouseholdBudgetMate.Application/Helpers/ExpenseActualAmountCalculator.cs:7` - effective actual amount rule.
- `src/HouseholdBudgetMate.Application/Helpers/BudgetHelper.cs:14` - closed-month write guard.
- `src/HouseholdBudgetMate.Domain/EntityConfiguration/AccountMonthBalanceConfiguration.cs:30` - unique account/month balance index.
- `src/HouseholdBudgetMate.Domain/EntityConfiguration/ExpenseConfiguration.cs:67` - unique regular expense generated-row index.
- `src/HouseholdBudgetMate.Domain/EntityConfiguration/IncomeConfiguration.cs:56` - unique regular income generated-row index.
- `src/HouseholdBudgetMate.Application/Services/Backup/BackupRestoreExecutor.cs:316` - backup restore inserts expenses before line items.
- `src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:272` - line-item validator lacks amount rule.

## Architecture Insights

The current architecture is service-centric by design: UI calls application services, application services own workflow, and domain entities are persistence-oriented. A domain refactor should therefore begin by extracting named policies/read-model builders inside Application or a carefully introduced Domain service layer, rather than immediately converting EF entities into rich aggregates.

The most promising boundary is `MonthlyFinancialPicture`: a projection/policy object that can name the relationship between `MonthPlan`, expense KPI, live balance, complete balance base, due savings transfers, and read-only lifecycle. This would reduce scattered formulas while preserving current contracts.

The second boundary is `EffectiveExpenseActual`: a named policy around parent actual vs line-item sum. It needs a deliberate decision on final-line-item deletion, backup restore recalculation, and negative line-item amounts before becoming a stricter invariant.

## Historical Context

- `context/domain/01-domain-distillation.md` - Source distillation and ranking; correct core target, but now partially corrected for existing EF unique indexes.
- `context/archive/2026-05-29-verify-monthly-safe-to-spend-loop/acceptance-evidence.md` - Current accepted monthly loop and numeric oracle.
- `context/archive/2026-06-02-testing-cross-screen-monthly-consistency/research.md` - Cross-screen consistency context and warning that screens have different responsibilities.
- `context/archive/2026-06-12-refactor-opportunities/research.md` - Prior identification of line-item actual amount as intentional but scattered.
- `context/archive/2026-06-12-refactor-opportunities/plan.md` - Warning that Plan page reload behavior is load-bearing.

## Related Research

- `context/changes/post-flow-analysis/research.md`
- `context/archive/2026-06-02-testing-cross-screen-monthly-consistency/research.md`
- `context/archive/2026-06-12-refactor-opportunities/research.md`
- `context/archive/2026-06-03-improve-monthly-planning/research.md`

## Open Questions

- Should `MonthlyFinancialPicture` live as an application read-model builder/policy first, or should it introduce richer domain types immediately?
- Should live balance continue to trust persisted `Expense.ActualAmount`, or should every projection use the same effective-actual policy?
- Is preserving parent actual after deleting the final line item still desired product behavior?
- Are negative line-item amounts valid refund/correction entries, or should they be rejected?
- Should savings-transfer UI controls visibly disable in closed months as part of this refactor, or stay as a separate UI polish issue?
- Should soft-deleted generated regular items permanently block regeneration, as current `IgnoreQueryFilters()` duplicate checks imply?
