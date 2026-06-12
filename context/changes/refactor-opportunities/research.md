---
date: 2026-06-12T20:58:36+02:00
researcher: Codex
git_commit: 708884d6b9c6af997c60a1ba75ab1f2ec1e2fcf2
branch: main
repository: HouseholdBudgetMate
topic: "refactor-opportunities from post-flow-analysis"
tags: [research, codebase, refactor, plan-page, expense-service, migrations]
status: complete
last_updated: 2026-06-12
last_updated_by: Codex
---

# Research: refactor-opportunities from post-flow-analysis

**Date**: 2026-06-12T20:58:36+02:00
**Researcher**: Codex
**Git Commit**: 708884d6b9c6af997c60a1ba75ab1f2ec1e2fcf2
**Branch**: main
**Repository**: HouseholdBudgetMate

## Research Question

Use `context/changes/post-flow-analysis/research.md` as the evidence base. List every problem that report records, classify which ones are structural refactor candidates, then explore each candidate in code and history. Produce a ranked set of refactor opportunities with trade-offs, without changing code and without deciding the implementation scope.

## Candidate Classification

Source problems from `context/changes/post-flow-analysis/research.md`:

| # | Reported problem | Classification | Reason |
|---|---|---|---|
| 1 | Dominant post-save pattern in `PlanPage` is a coarse `LoadAsync` reload; create/update return `ExpenseDto` but UI ignores it. | KANDYDAT | A fix would change UI state/refresh structure or service result shape. |
| 2 | UI save behavior is duplicated across `PlanPage` partials: service call, snackbar, cleanup, reload/refresh variants. | KANDYDAT | A fix would change component organization and shared post-save orchestration. |
| 3 | No literal `Post` domain vocabulary; product/UI sometimes says `wpis`, code says `Expense`. | KANDYDAT, but likely deferred | If this means a new cross-type concept, the fix changes domain vocabulary/abstractions. If it is only UI copy/search wording, it is not a structural refactor. |
| 4 | Test coverage is strong at service/E2E ends but thinner in the middle; no broad behavioral `PlanPage` create/edit component test. | NOT A CANDIDATE | Missing tests are not a refactor by themselves. Keep as feasibility/prerequisite input. |
| 5 | Line-item semantics are split between parent `Expense.ActualAmount`, UI zeroing, service recalculation, and DTO mapping. | KANDYDAT | A fix could centralize the effective-actual invariant or clarify storage semantics. |
| 6 | Save side effects are implicit: user scope/timestamps in `ApplicationDbContext`, audit via interceptor, query filters. | KANDYDAT | A fix could name/save-boundary guard these side effects or change save orchestration. |
| 7 | Model/migration changes have high-noise blast radius and should be avoided for pure flow changes. | KANDYDAT, mostly constraint | Managing this may require process/architecture boundaries; schema refactor itself should be deferred unless unavoidable. |

Non-candidate inputs retained for feasibility:

- Missing middle-layer UI tests lower confidence for `PlanPage` behavior-preserving refactors, although current test project has bUnit available (`src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj:12`).
- The repo has no `.github/workflows`; test-plan says local + CI gates, but repo inspection found only `.github/instructions/copilot-instructions.md`, so actual CI enforcement is unknown.
- Documentation and deploy guidance already warn that migrations on real data require backup, review, and restore smoke evidence (`context/foundation/deploy-plan.md:30`, `context/foundation/deploy-plan.md:166`, `README.md:137`).

## Summary

The strongest opportunity is not to replace full reloads immediately. The safest target shape is a named, local `PlanPage` post-save orchestration/refresh policy that initially preserves `LoadAsync` behavior while removing drift across handlers. This addresses accidental duplication without disturbing the load-bearing consistency mechanism.

The second opportunity is to make line-item `ActualAmount` semantics explicit and test-first. The rule is documented and partially protected, but the implementation lives in UI, service, persisted parent state, and mapping. A small domain/application helper or contract around "effective actual amount" would reduce future risk; changing the persistence model would be a separate, higher-risk design decision.

The third opportunity is a guardrail refactor around save side effects: preserve the existing `SaveChangesAsync` stamping/audit/query-filter model, but make save-boundary expectations explicit before any batching or local-refresh work changes audit/timestamp shape.

Vocabulary (`wpis` vs `Expense`) and model/migration blast radius should not be first implementation targets. They are either product/domain redesign or constraints to enforce around other refactors.

## Detailed Findings

### A. Coarse Post-Save Reload

Current shape:

- evidence: `LoadAsync` is the central page-state reload hub and refreshes categories, tag usage, accounts, preparation state, month plan, dashboard summary, incomes, live balance, chart/KPI state, query-driven edit/add state, and dirty state (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:25`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:31`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:59`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:93`).
- evidence: create expense ignores the returned `ExpenseDto`, resets form state, calls `LoadAsync`, then shows success (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:124`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:126`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:127`).
- evidence: edit expense also ignores the returned `ExpenseDto`, cancels edit, calls `LoadAsync`, then shows success (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:211`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:212`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:213`).
- evidence: `IExpenseService` already returns DTOs for create/update expense and line-item create/update, while delete/reorder return `Task` and copy/suggestions return `int` (`src/HouseholdBudgetMate.Abstractions/Interfaces/IExpenseService.cs:33`, `src/HouseholdBudgetMate.Abstractions/Interfaces/IExpenseService.cs:43`, `src/HouseholdBudgetMate.Abstractions/Interfaces/IExpenseService.cs:47`, `src/HouseholdBudgetMate.Abstractions/Interfaces/IExpenseService.cs:48`, `src/HouseholdBudgetMate.Abstractions/Interfaces/IExpenseService.cs:57`).
- evidence: suggestions deliberately use `LoadAsync(bypassPreparation: true)`, so reload behavior already has meaningful variants (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:446`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:482`).
- inference: the current service abstraction is DTO-capable, but UI consistency relies on re-reading all dependent projections rather than locally patching `_monthPlan`.
- unknown: whether a narrower refresh can defer dashboard/live-balance updates, or must update all projections synchronously after every save.

Intentionality verdict: conscious current consistency constraint, originally pragmatic shape.

- evidence: product scope requires current month state, `Live balance`, plan progress, and savings context to remain trustworthy (`context/foundation/prd.md:60`, `context/foundation/prd.md:95`).
- evidence: `context/archive/2026-05-29-verify-monthly-safe-to-spend-loop/plan.md:15` treats post-save `LoadAsync` as existing behavior to verify.
- evidence: `git log -L 25,63:src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs` shows `f19ab69 PlanPage refactor` introduced the partialized `LoadAsync`, and `82cbb36 feat(planning): improve monthly planning` later added `bypassPreparation`.

Feasibility notes:

- evidence: existing numeric guards are service-first: `MonthlyBudgetingLoopTests` and test-plan cookbook point at service projection agreement (`src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:25`, `context/foundation/test-plan.md:96`).
- evidence: UI contract tests inspect source and wiring; rendered smoke exists but does not drive real `PlanPage` save interactions (`src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:13`, `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopRenderedTests.cs:15`).
- inference: first safe step is a local `RefreshAfterSaveAsync`/refresh-policy wrapper that still delegates to `LoadAsync`, followed by tests/inventory before any local state patching.
- first prerequisite: inventory mutation handlers by refresh mode: full load, bypass-preparation load, no current-month refresh, and line-item re-expand.

### B. Duplicated Save Behavior Across `PlanPage` Partials

Current shape:

- evidence: expense create/edit/delete follow service call + local cleanup + `LoadAsync` + snackbar variants (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:124`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:211`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:237`).
- evidence: income create/edit/delete repeat the same family shape (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Incomes.cs:103`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Incomes.cs:160`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Incomes.cs:188`).
- evidence: savings transfer create/edit repeats service call, cleanup, reload, snackbar (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.SavingsTransfers.cs:27`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.SavingsTransfers.cs:79`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.SavingsTransfers.cs:83`).
- evidence: line-item handlers add a post-reload expansion preservation step (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:59`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:70`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:117`).
- evidence: not every save-like action should reload the current month; copying selected expenses to another target month clears copy state and shows snackbar without `LoadAsync` because source month state may not change (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:347`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:358`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:367`).
- inference: this is duplication around a family of local workflows, not one identical template.
- unknown: whether helper extraction should live only inside `PlanPage` partials or become a reusable UI service; current evidence supports local helper first.

Intentionality verdict: accidental complexity around a load-bearing pattern.

- evidence: `f19ab69 PlanPage refactor` split a 2315-line `PlanPage.razor` into partials and added `PlanPage.Expenses.cs`, `PlanPage.Incomes.cs`, `PlanPage.LineItems.cs`, `PlanPage.SavingsTransfers.cs`, and lifecycle/tag/input files.
- inference: the partial split was an intentional structural improvement, but no evidence was found that duplicated handler ceremony is itself intentional.

Feasibility notes:

- evidence: blast radius can stay inside `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/*` if the helper preserves behavior.
- evidence: test project has bUnit, xUnit, FluentAssertions, EF InMemory/SQLite, Moq, and NetArchTest (`src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj:12`, `src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj:17`, `src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj:18`, `src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj:19`, `src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj:21`, `src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj:22`).
- inference: a small helper for try/catch + error snackbar + refresh mode is feasible. A monolithic command pipeline would risk hiding the meaningful differences between handlers.
- first prerequisite: create a save-handler inventory table before implementation and preserve each handler's current refresh mode.

### C. `wpis` Vocabulary vs `Expense` Code Vocabulary

Current shape:

- evidence: persistence is explicitly expense-shaped: `ApplicationDbContext` has `DbSet<Expense>` and `DbSet<ExpenseLineItem>` (`src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:30`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:31`).
- evidence: domain aggregate is `Expense`, with planned/actual amounts, category/tag, soft delete, and line items (`src/HouseholdBudgetMate.Domain/Entities/Expense.cs:5`, `src/HouseholdBudgetMate.Domain/Entities/Expense.cs:16`, `src/HouseholdBudgetMate.Domain/Entities/Expense.cs:17`, `src/HouseholdBudgetMate.Domain/Entities/Expense.cs:27`).
- evidence: service seam is `IExpenseService`, not an entry/post service (`src/HouseholdBudgetMate.Abstractions/Interfaces/IExpenseService.cs:6`).
- evidence: `PlanPage` user flow says "Dodaj wydatek", not a neutral post/entry concept (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:1046` submit handler anchors the create expense form).
- inference: a mechanical code rename from `Expense` to `Post` would fight current business boundaries and produce high churn.
- unknown: whether future product vocabulary wants "wpis" to mean only a displayed expense row or a broader concept across expenses, incomes, line items, savings transfers, and audit rows.

Intentionality verdict: mostly vocabulary drift; no evidence of missing `Post` domain.

- evidence: PRD/roadmap model the core monthly loop through planned/actual/unexpected expenses, `Live balance`, savings, and month lifecycle (`context/foundation/roadmap.md:26`).
- inference: this should not be implemented as a refactor until product language defines a new business concept.

Feasibility notes:

- first prerequisite: decide whether `wpis` is display synonym or new aggregate. If new aggregate, this is a later domain analysis, not this refactor opportunity.
- recommended disposition: defer.

### D. Line-Item `ActualAmount` Semantics

Current shape:

- evidence: domain docs define the intended rule: when category allows line items, expense `ActualAmount` is `SUM(ExpenseLineItem.Amount)`; when no line items exist, it is entered manually (`context/foundation/domain.md:145`, `context/foundation/domain.md:148`, `context/foundation/domain.md:149`).
- evidence: UI zeroes parent actual amount when selected category/tag supports line items for create and edit (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:117`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:206`).
- evidence: UI disables actual amount input for line-item-capable selections or existing expenses with line items (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:712`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:784`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:1049`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:1112`).
- evidence: mapping exposes `ExpenseDto.ActualAmount` as line-item sum when any line items exist; otherwise it uses persisted parent actual (`src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs:23`).
- evidence: update service only accepts parent `ActualAmount` when no line items exist, then calls recalculation and saves again (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2271`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2280`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2281`).
- evidence: line-item create/update/delete each save the line-item mutation, recalculate parent actual, then save again (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2087`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2091`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2176`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2178`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2222`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2225`).
- evidence: recalculation returns without changing parent actual when no line items remain (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2736`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2746`).
- inference: the rule is intentional, but implementation is spread across UI, application service, persisted entity state, and mapping.
- unknown: whether parent `Expense.ActualAmount` should remain a persisted cache, become purely manual-only state, or be ignored whenever line-item support is enabled.

Intentionality verdict: conscious constraint / load-bearing decision.

- evidence: docs explicitly state the rule (`context/foundation/domain.md:148`).
- evidence: `git log -L 2228,2281:src/HouseholdBudgetMate.Application/Services/ExpenseService.cs` traces the "ignore parent actual when line items exist and recalculate" behavior back to `8019f74 impove categories`, then later hardening.

Feasibility notes:

- evidence: service tests already cover line-item create/update/delete recalculation (`src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:3281`, `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:3399`, `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:3483`).
- inference: first safe step is not schema change. It is a named helper/contract in application mapping/service for "effective actual amount" plus explicit tests for update-parent-with-existing-line-items and last-line-item deletion behavior.
- first prerequisite: pin the intended last-line-item-deleted behavior, because current code leaves parent actual unchanged when line-item count becomes zero.

### E. Implicit Save Side Effects

Current shape:

- evidence: `ApplicationDbContext.SaveChangesAsync` calls `UpdateTimestampsAndUserScope` before EF save (`src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:59`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:61`).
- evidence: save-time stamping covers financial and user-scoped entities including `Expense`, `ExpenseLineItem`, `Income`, `MonthPlan`, `MonthSavingsTransferItem`, accounts, annual plans, loans, and regular definitions (`src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:69`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:73`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:74`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:75`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:77`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:78`).
- evidence: query filters constrain budget-owner visibility and soft-deleted expenses (`src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:213`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:219`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:220`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:226`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:227`).
- evidence: audit interception is save-driven and treats `Expense`, `Income`, `Account`, `AccountMonthBalance`, `ExpenseLineItem`, `MonthSavingsTransferItem`, `Category`, `LoanInstallment`, and regular definitions as auditable (`src/HouseholdBudgetMate.Application/Auditing/AuditSaveChangesInterceptor.cs:26`, `src/HouseholdBudgetMate.Application/Auditing/AuditSaveChangesInterceptor.cs:34`, `src/HouseholdBudgetMate.Application/Auditing/AuditSaveChangesInterceptor.cs:147`).
- inference: changing save batching or replacing reloads with local patching can change timestamps, audit record count/shape, and visibility assumptions.
- unknown: whether current multi-save audit granularity is product-required or incidental.

Intentionality verdict: conscious load-bearing decision, with hidden complexity risk.

- evidence: architecture guide documents automatic timestamp handling in `ApplicationDbContext` (`context/foundation/architecture/architecture-guide.md:70`).
- evidence: `git log -L 59,80:src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs` shows user scope/timestamp behavior introduced and hardened across `7316ba7`, `fd579c3`, `82cbb36`, and `0ee7bdf`.
- evidence: audit trail was introduced by `5c1f102 Adds audit trail for entity changes and admin audit UI`.

Feasibility notes:

- evidence: tests already exist around user scoping and audit behavior (`src/HouseholdBudgetMate.Tests/Tests/Services/UserScopingTests.cs`, `src/HouseholdBudgetMate.Tests/Tests/Services/AuditTrailTests.cs`).
- inference: first step should be guardrail documentation/tests around save-boundary expectations, not batching changes.
- first prerequisite: if any later refactor changes number/order of `SaveChangesAsync`, add targeted audit-shape tests first.

### F. Model/Migration Blast Radius

Current shape:

- evidence: schema state spans domain entities, entity configuration, `ApplicationDbContext`, migration files, and model snapshot (`src/HouseholdBudgetMate.Domain/Entities/Expense.cs:5`, `src/HouseholdBudgetMate.Domain/EntityConfiguration/ExpenseConfiguration.cs:7`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:30`).
- evidence: `82cbb36 feat(planning): improve monthly planning` touched contracts, `IExpenseService`, `ExpenseService`, validators, `AnnualPlan` domain/config, `ApplicationDbContext`, migration, snapshot, tests, `PlanPage`, and `Statistics`.
- evidence: README states `dotnet ef migrations remove` is safe mainly before a migration is applied to important data and warns not to treat file removal as data rollback (`README.md:137`).
- evidence: deploy plan requires backup/review/restore notes for meaningful migrations and warns that app rollback does not roll back PostgreSQL schema/data (`context/foundation/deploy-plan.md:30`, `context/foundation/deploy-plan.md:166`, `context/foundation/deploy-plan.md:183`).
- inference: this is less a refactor target than a boundary condition: first refactor slice should explicitly avoid model/migration edits.
- unknown: whether generated EF snapshot noise is reviewed separately in practice; repo has policy docs/tests but no visible CI workflow.

Intentionality verdict: conscious constraint; EF noise is inherent, migration gates are intentional.

Feasibility notes:

- first prerequisite: declare "no model change" for the first implementation slice unless planning intentionally chooses a schema-bearing candidate.
- recommended disposition: use as constraint for A/B/D/E, not as standalone first refactor.

## Code References

- `context/changes/post-flow-analysis/research.md` - source evidence report and problem list.
- `context/map/repo-map.md` - monthly planning hot path and co-change risk.
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:25` - central reload hub.
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:124` - create expense ignores returned DTO and reloads.
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:211` - edit expense ignores returned DTO and reloads.
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Incomes.cs:103` - income save pattern.
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:59` - line-item save pattern.
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.SavingsTransfers.cs:27` - savings transfer save pattern.
- `src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs:23` - line-item sum drives DTO actual amount.
- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2271` - update ignores parent actual when line items exist.
- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2736` - line-item recalculation helper.
- `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:59` - implicit save-time stamping entry point.
- `src/HouseholdBudgetMate.Application/Auditing/AuditSaveChangesInterceptor.cs:147` - auditable entity list.
- `src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj:12` - bUnit is available for test prerequisites.
- `context/foundation/deploy-plan.md:166` - migration backup/review/restore gate.

## Architecture Insights

- `PlanPage` partialization was already one structural refactor. Another broad extraction should be justified by concrete drift reduction, not aesthetics.
- The simple architecture guide prefers direct UI -> application service calls and explicitly rejects MediatR/CQRS-style orchestration. Refactor targets should stay small and local unless planning chooses a larger domain redesign.
- Full reload currently acts as a consistency contract across month plan, dashboard summary, incomes, live balance, tags, accounts, chart/KPI state, and dirty state.
- Line-item actual amount is a business invariant, not only code duplication. Any cleanup that changes parent/child amount semantics needs product-level confirmation and tests first.
- DbContext and interceptor side effects are security/audit infrastructure. Treat them as no-touch constraints for UI refactor slices unless the plan explicitly targets save-boundary semantics.

## Historical Context

- `f19ab69 PlanPage refactor` split the original large `PlanPage.razor` into partials and introduced the current `LoadAsync` partial shape.
- `82cbb36 feat(planning): improve monthly planning` added month preparation, suggestions, annual planning, explicit target copy, and `LoadAsync(bypassPreparation: true)`, increasing both value and complexity of the current reload policy.
- `8019f74 impove categories` introduced the key line-item parent actual behavior visible in `git log -L` for `UpdateExpenseAsync`.
- `7316ba7`, `fd579c3`, and `0ee7bdf` evolved save-time user scope/stamping in `ApplicationDbContext`.
- `5c1f102 Adds audit trail for entity changes and admin audit UI` introduced audit interception.
- `context/archive/2026-06-02-testing-cross-screen-monthly-consistency/research.md` established service projection integration plus UI contract tests as the cheapest protection for monthly consistency.
- `context/archive/2026-06-03-improve-monthly-planning/research.md` showed that S-03 added substantial monthly planning behavior in the same hot path.

## Related Research

- `context/changes/post-flow-analysis/research.md`
- `context/map/repo-map.md`
- `context/archive/2026-06-03-improve-monthly-planning/research.md`
- `context/archive/2026-06-02-testing-cross-screen-monthly-consistency/research.md`
- `context/archive/2026-05-29-verify-monthly-safe-to-spend-loop/acceptance-evidence.md`
- `context/archive/2026-05-26-align-safe-to-spend-contract/plan.md`

## Refactor Opportunities

### 1. Normalize `PlanPage` post-save orchestration, preserving full reload first

Current -> target shape:

- Current: each partial owns its own try/catch, cleanup, `LoadAsync`/variant, expansion repair, and snackbar.
- Target: local `PlanPage` helper or refresh policy names the post-save modes: full load, bypass-preparation load, no source refresh, line-item re-expand. Initial implementation should still call existing `LoadAsync` paths.

Why it ranks here:

- evidence: duplication is accidental complexity, while the full reload is load-bearing. This target reduces drift without changing consistency semantics.
- evidence: blast radius can stay in `PlanPage.*` partials.
- inference: it has the best cost/benefit ratio because it prepares future narrower refresh work without requiring service contract, domain, audit, or migration changes.

Blast radius:

- Primary: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.*`.
- Secondary tests: `MonthlyBudgetingLoopUiTests`, possibly a focused bUnit helper/host test if planning chooses one.
- Avoid: `ExpenseService`, contracts, domain, migrations.

Incremental path:

1. Inventory all mutation handlers and classify refresh mode.
2. Add a behavior-preserving helper around error snackbar + refresh mode + success snackbar.
3. Move one low-risk handler family first, probably incomes or savings transfers.
4. Move expenses and line items after preserving special cases: bypass preparation, copy target no-refresh, line-item re-expand.

First prerequisite:

- Save-handler inventory with current behavior, including no-refresh and bypass cases.

### 2. Centralize and protect line-item effective actual amount semantics

Current -> target shape:

- Current: UI zeroes/disabled parent actual, service conditionally ignores parent actual and recalculates after line-item writes, mapping computes DTO actual from line-item sum.
- Target: named application-level helper/contract for effective actual amount and recalculation semantics, with tests pinning parent-update and last-line-item-deleted behavior. No schema change in first slice.

Why it ranks here:

- evidence: the rule is documented and load-bearing, but implementation is spread across layers.
- evidence: existing tests already cover parts of recalculation, lowering prerequisite cost.
- inference: debt cost is meaningful because future changes to UI refresh, copy, history, statistics, or audit can accidentally use the wrong actual amount.

Blast radius:

- Primary: `ExpenseService`, `ExpenseExtensionMapping`, `ExpenseServiceTests`.
- Secondary: `PlanPage.Expenses.cs` only if UI naming/disable logic is clarified.
- Avoid first: entity/config/migration changes.

Incremental path:

1. Add tests for update-parent-with-existing-line-items and last-line-item-deleted semantics.
2. Extract a small helper or static method for effective actual calculation used by mapping and service where appropriate.
3. Keep persisted `Expense.ActualAmount` behavior unchanged.
4. Only later consider whether parent actual should be a derived cache or manual-only field.

First prerequisite:

- Decide and pin expected behavior when the final line item is deleted.

### 3. Add save-boundary guardrails before any batching or local-state refresh

Current -> target shape:

- Current: `SaveChangesAsync` implicitly stamps user scope/timestamps and audit interceptor records financial changes; several service flows call save twice per logical action.
- Target: explicit tests/docs naming which save boundaries are intentional, especially for line-item recalculation and audit shape. No batching in first slice.

Why it ranks here:

- evidence: save side effects are conscious security/audit infrastructure.
- inference: the refactor value is mostly preventative: it lowers the risk of opportunity 1 or 2 accidentally changing audit/timestamp behavior.
- trade-off: it has less direct readability payoff than opportunity 1, so it ranks third.

Blast radius:

- Primary: tests around `AuditTrailTests`, `UserScopingTests`, and service methods if later batching is considered.
- Avoid first: changing `ApplicationDbContext`, interceptor behavior, or save counts.

Incremental path:

1. Identify service operations where multiple saves are part of current observable behavior.
2. Add a targeted audit-shape test only for operations a planned refactor may affect.
3. Document no batching/change-of-save-count unless a plan explicitly accepts changed audit/timestamp shape.

First prerequisite:

- If opportunity 2 touches line-item save/recalculate code, add audit/timestamp expectation tests first.

## Considered And Rejected Or Deferred

### Defer: rename or generalize `wpis` / `Post`

- evidence: active product/domain language is expense-oriented, and no `Post` domain exists.
- inference: if `wpis` is just UI language, a code refactor is unnecessary. If it is a new umbrella concept, the real work is business/domain redesign, outside this exploration.
- decision for ranking: reject as current refactor opportunity; keep as future product-language analysis.

### Defer: schema/model migration refactor

- evidence: migration and deploy docs intentionally treat schema/data changes as high-risk with backup/restore gates.
- inference: first refactor slices can improve UI/application structure without touching entities or migrations.
- decision for ranking: use as constraint, not an opportunity.

### Defer: replace full reload with local optimistic patching

- evidence: returned DTOs exist, but `LoadAsync` also refreshes dashboard, incomes, live balance, tag usage, accounts, chart/KPI, preparation and dirty state.
- inference: local patching is a possible later optimization, but first step should be naming and consolidating refresh policy while preserving behavior.
- decision for ranking: do not start here.

## Open Questions

- Is the current no-refresh behavior after copying expenses to another target month intentionally sufficient, or should the target-month archive/cache refresh become part of a named post-save mode?
- Should final-line-item deletion leave parent `ActualAmount` at the last calculated value, reset it to zero, or return to manual-entry semantics?
- Are audit log counts and intermediate recalculation audit entries part of user-visible financial history, or incidental implementation detail?
- Is `wpis` intended only as Polish UI wording, or a future cross-type business concept?
