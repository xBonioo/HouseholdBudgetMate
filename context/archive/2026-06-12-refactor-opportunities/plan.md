# Refactor Opportunities Implementation Plan

## Overview

This plan turns the `refactor-opportunities` research into a staged, implementation-ready refactor roadmap. The first deliverable is a behavior-preserving `PlanPage` post-save orchestration cleanup: name the refresh modes, reduce handler drift, and keep today's full reload semantics intact. Later phases address the second and third ranked opportunities: line-item effective actual amount semantics and save-boundary guardrails.

The plan intentionally starts with evidence and guardrails because the hot path is both valuable and fragile: monthly planning depends on `PlanPage`, `ExpenseService`, DTO contracts, UI refresh state, line-item behavior, audit, and user-scope stamping.

## Current State Analysis

`PlanPage` has a dominant post-save pattern where mutation handlers call an application service, clean up local state, call `LoadAsync`, then show a snackbar. The current full reload is load-bearing because `LoadAsync` refreshes categories, tag usage, accounts, month preparation, month plan, dashboard summary, incomes, live balance, chart/KPI state, query-driven edit/add state, and dirty tracking.

The duplication is not one perfectly identical template. Expenses include create/edit/delete, reorder, target-month copy, and month-preparation suggestions. Line items need re-expansion after reload. Suggestion apply/skip uses `LoadAsync(bypassPreparation: true)`. Copying to another month clears copy state and shows success without reloading the source month.

Line-item actual amount is a separate but adjacent risk. The current rule is intentional: when line items exist, effective actual amount comes from the line-item sum; when no line items exist, parent `Expense.ActualAmount` is manual. That behavior is currently spread across UI, `ExpenseService`, mapping, persisted parent state, and tests.

Save side effects are also load-bearing. `ApplicationDbContext.SaveChangesAsync` stamps timestamps and user scope, query filters enforce budget-owner visibility, and `AuditSaveChangesInterceptor` records financial entity changes. Any refactor that changes save ordering or batching could change audit/timestamp shape, so this plan protects touched paths before changing save boundaries.

## Desired End State

By the end of the plan:

- `PlanPage` mutation handlers use a named local post-save orchestration pattern instead of repeating ad hoc try/catch, cleanup, reload, expansion repair, and snackbar flow.
- The first behavior-preserving slice keeps full reload semantics and all current reload variants intact: full load, `bypassPreparation`, target-copy no-refresh, and line-item re-expand.
- Expense handlers are refactored first, then remaining `PlanPage` save families adopt the same pattern.
- Line-item effective actual amount semantics are pinned by tests and centralized only in a no-schema-change way.
- Save-boundary expectations for touched paths are explicit enough that future batching or local-refresh changes cannot accidentally rewrite audit/timestamp behavior.
- Browser smoke evidence exists for the critical monthly save flows after the refactor lands.

### Key Discoveries:

- `LoadAsync` is the central page-state reload hub and refreshes multiple dependent projections (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:25`).
- Expense create/edit currently ignore returned `ExpenseDto` and rely on `LoadAsync` for consistency (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:124`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:211`).
- Suggestion flows deliberately use `LoadAsync(bypassPreparation: true)` (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:446`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:482`).
- Target-month copy is a save-like action that intentionally does not reload the source month (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:347`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:367`).
- Line-item DTO actual amount is derived from line-item sum when any line items exist (`src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs:23`).
- `ExpenseService.UpdateExpenseAsync` ignores parent actual input when line items exist and recalculates after saving (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2271`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2280`).
- `SaveChangesAsync` has implicit timestamp/user-scope side effects (`src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:59`), and audit is interceptor-driven (`src/HouseholdBudgetMate.Application/Auditing/AuditSaveChangesInterceptor.cs:147`).
- Current UI contract tests are mostly static, but bUnit is available in the test project (`src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj:12`).
- Existing Playwright specs cover expense create and cross-screen expense edit, but not the full refactor smoke matrix (`e2e/seed.spec.ts`, `e2e/cross-screen-monthly-consistency.spec.ts`).

## What We're NOT Doing

- Not replacing `LoadAsync` with local optimistic DTO patching in the first `PlanPage` refactor slice.
- Not changing `IExpenseService`, DTO contracts, domain entities, EF configurations, migrations, or model snapshots in Phases 1-3.
- Not changing business rules for line-item `ActualAmount`.
- Not batching or reordering `SaveChangesAsync` calls in the `PlanPage` orchestration phases.
- Not renaming `Expense` to `Post` or introducing a new `wpis` aggregate.
- Not introducing MediatR, CQRS, command pipelines, or cross-layer UI services.
- Not treating Playwright as the primary regression layer; it is final smoke evidence, while service/static tests remain the cheaper gates.

## Implementation Approach

Use a staged refactor. First, document and test current behavior. Then introduce a small local `PlanPage` orchestration helper or refresh policy that keeps today's `LoadAsync` calls but makes refresh intent explicit. Apply it to expenses first because that is the hot path and carries the most special cases. Once the pattern is proven, extend it to incomes, savings transfers, and line items. Only after the UI orchestration slice is stable should the plan touch line-item effective actual semantics or save-boundary tests.

The implementation should preserve the repository's simple layered architecture: UI builds request DTOs and calls application services directly; application services own business rules and persistence; domain and migrations remain untouched unless a later plan explicitly accepts schema risk.

## Critical Implementation Details

### Refresh Modes Are Behavior, Not Decoration

The helper must preserve today's different refresh meanings: normal full reload, `bypassPreparation` reload, no current-month reload after target copy, and reload-plus-reexpand for line items. Do not collapse these into one generic "save succeeded" method unless the mode remains visible at each call site.

### Full Reload Stays The Consistency Boundary

Returned DTOs from create/update methods must not be used to locally patch `_monthPlan` in Phases 1-3. `LoadAsync` still updates dependent projections such as dashboard summary, incomes, live balance, tag usage, charts, and dirty state.

### Save Boundaries Stay Stable Until Tested

Do not batch the two-save line-item flows or update expense recalculation flow as part of the `PlanPage` refactor. If a later phase changes save count or ordering, it must first add audit/timestamp expectations for that operation.

## Phase 1: Baseline Inventory And Guardrails

### Overview

Create the behavior inventory and low-cost test guardrails needed before moving handlers to a shared post-save orchestration pattern. This phase should not refactor production behavior.

### Changes Required:

#### 1. Save Handler Inventory

**File**: `context/changes/refactor-opportunities/save-handler-inventory.md`

**Intent**: Record every save-like `PlanPage` handler, its preconditions, service call, local cleanup, refresh mode, expansion behavior, and snackbar outcome before implementation starts.

**Contract**: The inventory must classify at least these modes:

- full reload via `LoadAsync()`
- preparation-bypass reload via `LoadAsync(bypassPreparation: true)`
- target-copy no current-month reload
- reload plus line-item re-expand
- close/open month reload
- warning/validation early return with no service call

#### 2. Static UI Contract Guardrails

**File**: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`

**Intent**: Extend the existing source-level UI contract so future refactors preserve the named refresh modes and handler wiring.

**Contract**: Assertions should guard the presence of the inventory-relevant call paths in `PlanPage.Expenses.cs`, `PlanPage.Incomes.cs`, `PlanPage.SavingsTransfers.cs`, `PlanPage.LineItems.cs`, and `PlanPage.Lifecycle.cs`. The test should not assert exact helper implementation details that would make the refactor harder than necessary.

#### 3. Expense And Line-Item Service Guardrails

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs`

**Intent**: Pin existing line-item actual amount behavior that the `PlanPage` refactor must not accidentally change.

**Contract**: Add or strengthen tests for:

- updating a parent expense with existing line items ignores request parent `ActualAmount`
- deleting one of multiple line items recalculates parent actual to the remaining line-item sum
- deleting the final line item preserves the current documented behavior from the existing implementation; if the current behavior is "leave the last calculated parent actual unchanged", encode that explicitly

#### 4. Audit/Save Boundary Guard For Touched Paths

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/AuditTrailTests.cs`

**Intent**: Protect audit behavior for the expense/line-item operations that are most likely to be touched by later phases.

**Contract**: Add targeted assertions only for touched or soon-to-be-touched paths. The goal is not a full audit matrix; it is a tripwire if a later refactor changes save ordering or suppresses expected financial audit entries.

### Success Criteria:

#### Automated Verification:

- Inventory exists and names all refresh modes: `Test-Path context/changes/refactor-opportunities/save-handler-inventory.md`
- UI guard tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- Expense and audit guard tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~AuditTrailTests"`
- No production behavior changes are present outside inventory/test guardrails: review `git diff --stat`

#### Manual Verification:

- Review the inventory against `PlanPage.*` and confirm each handler has exactly one listed refresh mode.
- Confirm no handler-specific exception has been flattened into a generic mode before implementation starts.

**Implementation Note**: After completing this phase and all automated verification passes, pause for human review of the inventory before moving handler code.

---

## Phase 2: Expenses-First Post-Save Orchestration

### Overview

Introduce the local `PlanPage` post-save orchestration helper or refresh policy and apply it to expense-related handlers first. This is the highest-value and highest-risk UI slice, so it should prove the pattern without changing reload semantics.

### Changes Required:

#### 1. Local Post-Save Orchestration Helper

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs`

**Intent**: Provide a small local helper or helper family that names post-save refresh intent and centralizes repeated error/success handling where it does not hide important branch behavior.

**Contract**: The helper must support, at minimum:

- normal full reload
- preparation-bypass reload
- no current-month reload
- optional post-reload callback for line-item expansion in later phases

The helper remains private to `PlanPage` partials. It must not introduce a new app-wide UI service or change service contracts.

#### 2. Expense Create/Edit/Delete

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs`

**Intent**: Move expense create, edit, and delete success/error/reload ceremony onto the new local orchestration pattern while preserving all current preconditions, parsing, local cleanup, and snackbar messages.

**Contract**: These handlers still call the same `ExpenseService` methods, still discard returned `ExpenseDto`, still use full `LoadAsync()`, and still show the same success/error/warning outcomes.

#### 3. Expense Reorder

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs`

**Intent**: Preserve reorder-specific guardrails while reducing post-save reload drift.

**Contract**: Reorder must still block when filters are active and still use full reload after successful reorder. It must not start using local list reordering as the source of truth.

#### 4. Copy And Month Preparation Suggestions

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs`

**Intent**: Bring copy/suggestion flows into the named refresh policy without changing their special behavior.

**Contract**:

- Target-month copy keeps its no-current-month-reload behavior.
- Apply suggestions still calls `RefreshArchiveMonthsCacheAsync` and `LoadAsync(bypassPreparation: true)` in the current order unless implementation proves the order is already reversed elsewhere and tests cover it.
- Skip suggestions still uses `LoadAsync(bypassPreparation: true)`.
- Existing snackbar success/info/error messages remain semantically unchanged.

#### 5. UI Contract Updates For Helper Adoption

**File**: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`

**Intent**: Update static tests so they guard the resulting behavior rather than old duplicated code shape.

**Contract**: Tests should verify that the expense partial still exposes all required operations and refresh modes. They should not require every handler to contain literal `await LoadAsync()` after the refactor if the helper clearly owns that mode.

### Success Criteria:

#### Automated Verification:

- Expense-focused UI contract tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- Expense service guard tests still pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~AuditTrailTests"`
- Web project builds: `dotnet build HouseholdBudgetMate.slnx -c Release`
- Whitespace check passes: `git diff --check -- .`

#### Manual Verification:

- Review the expense handler diff against `save-handler-inventory.md` and confirm every listed mode is preserved.
- Confirm no `Abstractions`, `Application`, `Domain`, `Migrations`, or EF snapshot file changed in this phase except tests if deliberately touched in Phase 1.

**Implementation Note**: Pause after this phase for human review because expenses are the hottest monthly-loop path.

---

## Phase 3: Extend Pattern To Remaining PlanPage Saves

### Overview

Apply the proven local post-save pattern to incomes, savings transfers, and line items. This completes the primary `PlanPage` cleanup while preserving full reload semantics and line-item re-expansion.

### Changes Required:

#### 1. Income Handlers

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Incomes.cs`

**Intent**: Move income create/edit/delete onto the local orchestration pattern after the expenses-first slice proves the helper.

**Contract**: Preserve `EnsureMonthEditable`, delete confirmation, parsing, service calls, local cleanup, full reload, and existing snackbar text.

#### 2. Savings Transfer Handlers

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.SavingsTransfers.cs`

**Intent**: Normalize savings transfer create/edit/delete post-save behavior.

**Contract**: Preserve existing form reset/date reset, full reload, and snackbar behavior.

#### 3. Line-Item Handlers

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs`

**Intent**: Normalize line-item create/edit/delete while preserving post-reload expansion behavior.

**Contract**: After successful create/edit/delete and reload, the affected expense row must still be expanded when today's code expands it. No line-item actual amount business rules change in this phase.

#### 4. Final UI Contract Alignment

**File**: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`

**Intent**: Align static contract tests with the final local helper pattern across all `PlanPage` partials.

**Contract**: Tests should guard behavior-relevant strings/method wiring/refresh modes and avoid brittle assertions about exact duplicated code layout.

### Success Criteria:

#### Automated Verification:

- UI contract tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~MonthlyBudgetingLoopRenderedTests"`
- Monthly loop service tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- Expense, income, and audit service tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~IncomeServiceTests|FullyQualifiedName~AuditTrailTests"`
- Solution builds: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual Verification:

- Review all `PlanPage` partial diffs against the inventory and confirm no refresh mode changed.
- Confirm line-item expansion behavior is preserved after create/edit/delete.

**Implementation Note**: Pause after this phase for human review before moving into application-layer line-item cleanup.

---

## Phase 4: Effective Actual Amount Slice

### Overview

Address the second-ranked opportunity with a test-first, no-schema-change cleanup around effective actual amount. This phase may add a small application-level helper if it reduces duplication and clarifies the invariant, but it must not change business behavior or persistence schema.

### Changes Required:

#### 1. Line-Item Actual Amount Tests

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs`

**Intent**: Make current effective actual amount semantics explicit before extracting any helper.

**Contract**: Tests must pin:

- parent actual input is ignored when an expense already has line items
- effective DTO actual amount equals line-item sum when line items exist
- final-line-item deletion preserves the selected current behavior from Phase 1
- statistics/projection paths that depend on actual amount still read the expected value

#### 2. Effective Actual Amount Helper

**File**: `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs`

**Intent**: Reduce scattered effective-actual logic inside `ExpenseService` by naming the calculation or recalculation invariant.

**Contract**: Any helper must stay internal/private to the application layer unless a clear cross-file reuse need appears. It must not require a new abstraction contract, migration, or domain entity change.

#### 3. Mapping Alignment

**File**: `src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs`

**Intent**: Align DTO mapping with the named effective-actual invariant without changing DTO shape.

**Contract**: `ExpenseDto.ActualAmount` still reports line-item sum when line items exist and parent actual otherwise.

#### 4. UI Constraint Check

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs`

**Intent**: Keep the UI parent-actual disable/zeroing behavior consistent with the application invariant.

**Contract**: No new UI calculation should replace application-layer behavior. The UI may keep selection-based enable/disable helpers, but the service remains the authority.

### Success Criteria:

#### Automated Verification:

- Line-item and expense service tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests"`
- Monthly loop projection tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- Architecture tests pass if helper visibility/namespaces change: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~Architecture"`
- Solution builds: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual Verification:

- Review that no domain/entity/config/migration files changed.
- Review one line-item create/edit/delete scenario in UI or browser smoke before accepting the phase.

**Implementation Note**: If implementation discovers that behavior should change, stop and open a separate business/domain decision. Do not smuggle that change into this refactor.

---

## Phase 5: Save-Boundary Guardrails And Browser Evidence

### Overview

Close the plan by verifying audit/save boundaries for touched paths and collecting browser smoke evidence across the monthly save flows affected by the refactor.

### Changes Required:

#### 1. Save-Boundary Guardrails

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/AuditTrailTests.cs`

**Intent**: Ensure touched expense and line-item flows still produce expected audit behavior after helper/refactor work.

**Contract**: Tests should cover only operations touched by Phases 2-4. They should pin meaningful audit semantics, not every incidental timestamp field.

#### 2. User-Scope Regression Guard

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/UserScopingTests.cs`

**Intent**: Confirm refactored save paths still rely on application services and DbContext user-scope stamping rather than bypassing scoped persistence.

**Contract**: Add or reuse targeted tests only if implementation touched save/service code in Phase 4. If Phase 4 stayed private and behavior-preserving with existing coverage, this may be a verification-only step.

#### 3. Browser Smoke Evidence

**File**: `context/changes/refactor-opportunities/acceptance-evidence.md`

**Intent**: Record final manual/browser evidence for the refactored monthly save flows.

**Contract**: Evidence must include:

- command(s) run, build/test results, and date
- browser smoke notes for expense create/edit/delete
- income create/edit/delete
- savings transfer create/edit/delete
- line-item create/edit/delete with row re-expansion
- suggestion skip/apply with `bypassPreparation`
- target-month copy with no source-month reload regression

#### 4. Optional E2E Spec Extension

**File**: `e2e/cross-screen-monthly-consistency.spec.ts`

**Intent**: Extend browser automation only if the manual smoke reveals a stable, valuable assertion that belongs in Playwright.

**Contract**: Keep any Playwright addition narrow. The existing config requires a running app at `https://localhost:7135/` and authenticated storage state at `playwright/.auth/user.json`, so do not make Playwright the only acceptance gate.

### Success Criteria:

#### Automated Verification:

- Audit and user-scope tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~AuditTrailTests|FullyQualifiedName~UserScopingTests"`
- Full targeted monthly loop suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~MonthlyBudgetingLoopRenderedTests|FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~IncomeServiceTests"`
- Full test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`
- Solution builds: `dotnet build HouseholdBudgetMate.slnx -c Release`
- Diff check passes: `git diff --check -- .`

#### Manual Verification:

- Start the app and complete browser smoke for all flows listed in `acceptance-evidence.md`.
- If Playwright is available and auth state is prepared, run relevant browser checks such as `npx playwright test e2e/seed.spec.ts e2e/cross-screen-monthly-consistency.spec.ts --project=chromium`.
- Review final diff and confirm no unexpected `Abstractions`, `Domain`, `Migrations`, or EF snapshot changes landed.

**Implementation Note**: This phase closes the change only after evidence is recorded. If browser smoke finds a regression, fix it before archiving.

---

## Testing Strategy

### Unit Tests:

- `ExpenseServiceTests` guard line-item effective actual amount semantics, copy/suggestion behavior, and monthly expense operations.
- `IncomeServiceTests` guard income save behavior where monthly projections depend on income data.
- `AuditTrailTests` and `UserScopingTests` guard touched save boundaries.

### UI Contract Tests:

- `MonthlyBudgetingLoopUiTests` guards static `PlanPage` wiring, labels, refresh modes, no stale `Safe-to-spend` wording, and post-save orchestration structure.
- `MonthlyBudgetingLoopRenderedTests` remains a narrow rendered smoke layer for accepted monthly contract state.

### Integration Tests:

- `MonthlyBudgetingLoopTests` remains the primary numeric monthly-loop projection guard.
- Playwright remains final browser smoke evidence, not the first-line regression layer.

### Manual Testing Steps:

1. Create, edit, delete an expense and confirm the row/table/KPI refresh as before.
2. Reorder expenses with and without active filters; confirm filters still block reorder.
3. Copy selected expenses to a different target month and confirm the source month behavior remains stable.
4. Apply and skip historical suggestions; confirm preparation does not reappear after `bypassPreparation` flows.
5. Create, edit, delete an income and confirm plan/live balance projections refresh.
6. Create, edit, delete a savings transfer and confirm savings/live balance presentation refreshes.
7. Create, edit, delete a line item and confirm affected row re-expands and actual amount behavior remains unchanged.
8. Review audit/admin evidence if save-boundary tests changed.

## Performance Considerations

The first `PlanPage` refactor deliberately preserves full reload behavior, so it does not improve request count or latency. That is intentional: consistency is more important than premature optimization in this slice. A future local-patching optimization should start from the named refresh modes produced here and separately prove that dashboard summary, live balance, incomes, tag usage, charts, and dirty state stay correct.

## Migration Notes

No schema migration is planned. Phases 1-3 must not change `Abstractions`, `Application` production code, `Domain`, `Migrations`, or EF snapshots. Phase 4 may make a small application-layer helper change only if tests pin current behavior first. Any discovered need for schema changes stops this plan and requires a separate migration-focused change with backup/restore gates.

## References

- Related research: `context/changes/refactor-opportunities/research.md`
- Source analysis: `context/changes/post-flow-analysis/research.md`
- Main reload hub: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:25`
- Expense handlers: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:102`
- Income handlers: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Incomes.cs:84`
- Savings transfer handlers: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.SavingsTransfers.cs:9`
- Line-item handlers: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:36`
- Effective actual mapping: `src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs:23`
- Expense service recalculation: `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2736`
- Save side effects: `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:59`
- Audit side effects: `src/HouseholdBudgetMate.Application/Auditing/AuditSaveChangesInterceptor.cs:147`
- UI contract tests: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:13`
- Existing browser specs: `e2e/seed.spec.ts`, `e2e/cross-screen-monthly-consistency.spec.ts`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append `- <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Baseline Inventory And Guardrails

#### Automated

- [x] 1.1 Inventory exists and names all refresh modes: `Test-Path context/changes/refactor-opportunities/save-handler-inventory.md`
- [x] 1.2 UI guard tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- [x] 1.3 Expense and audit guard tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~AuditTrailTests"`
- [x] 1.4 No production behavior changes are present outside inventory/test guardrails: review `git diff --stat`

#### Manual

- [ ] 1.5 Review the inventory against `PlanPage.*` and confirm each handler has exactly one listed refresh mode
- [ ] 1.6 Confirm no handler-specific exception has been flattened into a generic mode before implementation starts

### Phase 2: Expenses-First Post-Save Orchestration

#### Automated

- [x] 2.1 Expense-focused UI contract tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- [x] 2.2 Expense service guard tests still pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~AuditTrailTests"`
- [x] 2.3 Web project builds: `dotnet build HouseholdBudgetMate.slnx -c Release`
- [x] 2.4 Whitespace check passes: `git diff --check -- .`

#### Manual

- [ ] 2.5 Review the expense handler diff against `save-handler-inventory.md` and confirm every listed mode is preserved
- [ ] 2.6 Confirm no `Abstractions`, `Application`, `Domain`, `Migrations`, or EF snapshot file changed in this phase except tests if deliberately touched in Phase 1

### Phase 3: Extend Pattern To Remaining PlanPage Saves

#### Automated

- [x] 3.1 UI contract tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~MonthlyBudgetingLoopRenderedTests"`
- [x] 3.2 Monthly loop service tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- [x] 3.3 Expense, income, and audit service tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~IncomeServiceTests|FullyQualifiedName~AuditTrailTests"`
- [x] 3.4 Solution builds: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual

- [ ] 3.5 Review all `PlanPage` partial diffs against the inventory and confirm no refresh mode changed
- [ ] 3.6 Confirm line-item expansion behavior is preserved after create/edit/delete

### Phase 4: Effective Actual Amount Slice

#### Automated

- [x] 4.1 Line-item and expense service tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests"`
- [x] 4.2 Monthly loop projection tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- [x] 4.3 Architecture tests pass if helper visibility/namespaces change: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~Architecture"`
- [x] 4.4 Solution builds: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual

- [ ] 4.5 Review that no domain/entity/config/migration files changed
- [ ] 4.6 Review one line-item create/edit/delete scenario in UI or browser smoke before accepting the phase

### Phase 5: Save-Boundary Guardrails And Browser Evidence

#### Automated

- [x] 5.1 Audit and user-scope tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~AuditTrailTests|FullyQualifiedName~UserScopingTests"`
- [x] 5.2 Full targeted monthly loop suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~MonthlyBudgetingLoopRenderedTests|FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~IncomeServiceTests"`
- [x] 5.3 Full test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`
- [x] 5.4 Solution builds: `dotnet build HouseholdBudgetMate.slnx -c Release`
- [x] 5.5 Diff check passes: `git diff --check -- .`

#### Manual

- [ ] 5.6 Start the app and complete browser smoke for all flows listed in `acceptance-evidence.md`
- [ ] 5.7 If Playwright is available and auth state is prepared, run relevant browser checks such as `npx playwright test e2e/seed.spec.ts e2e/cross-screen-monthly-consistency.spec.ts --project=chromium`
- [ ] 5.8 Review final diff and confirm no unexpected `Abstractions`, `Domain`, `Migrations`, or EF snapshot changes landed
