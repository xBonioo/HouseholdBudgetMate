# Improve Monthly Planning Implementation Plan

## Overview

Implement S-03 from the roadmap: a household member can prepare a month faster with expense-only copies, first-open historical suggestions from the same month in the previous year, preserved recurring auto-sync, year-level annual income/savings targets, and deviation alert candidates prepared for future notifications.

## Current State Analysis

PlanPage already supports a copy mode, but the copy target is hard-coded to the next calendar month. The service method behind it copies selected expenses, resets actual amounts to zero, and skips recurring duplicates in the target month.

Month opening currently has an important side effect: `ExpenseService.GetMonthAsync` creates a missing month and auto-syncs active recurring expenses, regular incomes, and loan installments. We are preserving that behavior. Historical suggestions must therefore appear before the first `GetMonthAsync` call creates the target plan, and applying or dismissing suggestions must make the recurring auto-sync behavior clear.

Statistics currently shows annual actuals and monthly finance rollups. It has no editable `Plan roczny` model, and annual finance rows are limited to months with actual expense spending. Year-level annual targets need new persistence and contracts rather than being squeezed into the existing actuals projection.

## Desired End State

When a user opens a monthly plan that does not yet exist, the page first shows a preparation step with selectable historical expense suggestions from the same month in the previous year. The user can adjust suggested planned amounts, confirm selected rows, or skip suggestions; only then is the month created and loaded. Active recurring expenses still auto-sync idempotently when the month is created.

The copy feature can copy selected expense plan rows to a chosen target month, not just the next month. Statistics includes a persisted year-level `Plan roczny` for expected annual income and expected annual savings, plus alert candidates for categories that exceed their historical average by more than 20%. No notifications are sent in this slice.

### Key Discoveries

- S-03 requires copy, history, recurring expense, alert-prep, and annual context improvements (`context/foundation/roadmap.md:115`).
- Current copy mode only targets the next month (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:273`).
- Current copy service skips duplicate recurring definitions and resets actual amounts (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1609`).
- `GetMonthAsync` auto-creates missing months and syncs recurring items (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:332`).
- Annual Statistics is actuals-focused and has no `Plan roczny` model (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:284`).
- Current MVP excludes a separate `Safe-to-spend` output (`context/foundation/prd.md:105`).

## Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Recurring behavior | Preserve existing auto-sync | Lowest regression risk and keeps current idempotent recurring behavior intact. | User |
| First-open suggestions | Show before creating a missing month | Matches the requested UX: suggestions appear when opening a plan that does not yet exist. | User |
| Suggestion source | Same month in previous year | Matches roadmap and user clarification. | User + Research |
| Suggestion confirmation | Selectable proposal list with editable price | Trust-preserving and avoids silent historical generation. | User |
| Suggested amount | Actual spent + 10%, rounded by scale | Matches roadmap's buffer and rounding guidance. | User + Plan |
| Rounding rule | Round up to nearest 10 under 500, nearest 100 at 500+ | Avoids under-planning after applying the buffer while avoiding false precision. | Plan |
| Copy scope | Expenses only | Matches existing copy path and avoids income/savings date semantics. | User |
| Annual plan | Persist year-level income and savings targets | Makes `Plan roczny` durable without overbuilding monthly grids. | User |
| Alert exclusions | None in this slice | Simpler foundation; future category exclusions can build on candidates. | User |
| Manual verification | Targeted browser smoke | Catches Blazor/MudBlazor interaction issues without making e2e a gate. | User + Test Plan |

## What We're NOT Doing

- Not changing the existing recurring auto-sync behavior into approval-only suggestions.
- Not copying incomes, savings transfers, or expense line items.
- Not adding monthly annual-target grids or category-level annual budgets.
- Not adding category exclusions for deviation alerts.
- Not sending real notifications.
- Not adding a separate `Safe-to-spend` value or KPI.
- Not adding full browser/e2e automation.
- Not changing the real-data readiness gate.

## Implementation Approach

Add preview/apply service contracts for missing-month preparation without creating the month during preview. The preview reads same-month-previous-year expenses and returns conservative, editable suggestions. The apply command creates the target month, preserves existing recurring auto-sync, then inserts selected historical expense rows with user-edited planned amounts and duplicate protection.

Extend the existing copy service to accept an explicit target month while preserving the current next-month wrapper for compatibility. Add a small annual-plan entity and expose it through Statistics. Add deviation alert candidates as service-provided data; show them as an informational foundation for future notifications.

## Critical Implementation Details

### Missing-Month Preview Must Not Create A Month

Do not call `GetMonthAsync` before the first-open preparation check. Add a service method that checks for an existing `MonthPlan` and builds suggestions with `AsNoTracking()` queries only. Creating a `MonthPlan` too early would hide the first-open suggestion step and pollute available-month/archive lists.

### Recurring Auto-Sync Remains Authoritative

When applying suggestions, let the existing month creation path sync active recurring expenses first. Historical suggestions linked to active recurring definitions, or obvious duplicates of auto-synced rows, must be skipped or marked unavailable so recurring rows are not duplicated.

### Historical Suggestions Are Expense Plans, Not Actuals

Suggestions use previous-year actual or planned amounts as the basis for the new planned amount. They must not copy actual amounts or line items into the target month.

### Annual Targets Are Separate From Actuals

Persisted annual plan targets are user-authored expectations. Do not reinterpret existing `MonthlyFinance` actual rows as annual targets.

## Phase 1: Backend Month Preparation And Copy Contracts

### Overview

Add service contracts for first-open preparation, selectable historical suggestions, editable suggested amounts, and explicit-target expense copy.

### Changes Required

#### 1. Month Preparation DTOs And Requests

**Files**:
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/MonthPlanPreparationDto.cs`
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/MonthPlanExpenseSuggestionDto.cs`
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Requests/ApplyMonthPlanSuggestionsRequest.cs`
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Requests/ApplyMonthPlanSuggestionItemRequest.cs`
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Requests/CopySelectedExpensesToMonthRequest.cs`

**Intent**: Define stable contracts for previewing a missing month and applying selected historical suggestions.

**Contract**: `MonthPlanPreparationDto` must carry target year/month, whether the month already exists, source year/month, and suggestion rows. Each suggestion row must carry source expense id, name, category/tag ids and labels, source planned/actual amounts, suggested planned amount, reason text, and an availability flag/reason when duplicate protection excludes it.

#### 2. Expense Service Interface

**File**: `src/HouseholdBudgetMate.Abstractions/Interfaces/IExpenseService.cs`

**Intent**: Expose missing-month preparation, suggestion application, and explicit-target copy to Blazor pages.

**Contract**: Add methods for `GetMonthPlanPreparationAsync(int year, int month, CancellationToken)`, `ApplyMonthPlanSuggestionsAsync(ApplyMonthPlanSuggestionsRequest, CancellationToken)`, and `CopySelectedExpensesToMonthAsync(CopySelectedExpensesToMonthRequest, CancellationToken)`. Keep `CopySelectedExpensesToNextMonthAsync` as a compatibility wrapper that delegates to the explicit-target method.

#### 3. Suggestion Algorithm

**File**: `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs`

**Intent**: Build conservative historical suggestions without creating the target month.

**Contract**: For a missing target month, query the same calendar month in the previous year. Suggest expense rows as selectable proposals when they are not obvious recurring duplicates. The suggested planned amount uses `basis = actual > 0 ? actual : planned`, then `basis * 1.10`, rounded up to the nearest 10 when below 500 and nearest 100 when 500 or above. Actual amounts and line items are never copied.

#### 4. Suggestion Application

**File**: `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs`

**Intent**: Create the month and insert only user-confirmed historical suggestions after recurring auto-sync has had a chance to populate active definitions.

**Contract**: Validate target year/month and selected source expense ids. Create/load the target month through the existing get-or-create path, require the target month open, then insert selected expense rows with submitted planned amounts, `ActualAmount = 0`, original category/tag/show-remaining, and appended ordering. Re-check duplicates after month creation because recurring auto-sync may have just inserted rows.

#### 5. Explicit-Target Expense Copy

**File**: `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs`

**Intent**: Generalize copy mode from next-month-only to selected target month.

**Contract**: `CopySelectedExpensesToMonthAsync` accepts source year/month, target year/month, and selected expense ids. It must reject copying into the same month, require source rows exist, require the target month open, preserve planned fields, reset actuals to zero, skip duplicate recurring definitions, and not copy line items.

#### 6. Service Tests

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs`

**Intent**: Pin the core service behavior before UI work.

**Contract**: Add tests for preview not creating a month, same-month-last-year suggestions, scale-based rounding, applying selected suggestions with edited amount, recurring duplicate suppression, explicit-target copy, same-month copy rejection, and line-item non-copy behavior.

### Success Criteria

#### Automated Verification

- Targeted ExpenseService tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests"`
- Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual Verification

- Review service tests and confirm expected suggestion amounts are literal oracle values, not copied from production helper logic.

**Implementation Note**: Stop after this phase if preserving recurring auto-sync appears to make historical suggestions impossible without duplicate risk; adjust duplicate rules before UI work.

---

## Phase 2: PlanPage First-Open Suggestions And Targeted Copy UX

### Overview

Update PlanPage so missing months show historical suggestions before month creation, and copy mode can choose a target month.

### Changes Required

#### 1. First-Open Preparation Flow

**Files**:
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs`
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.cs`

**Intent**: Insert the preparation preview before `GetMonthAsync` creates a missing month.

**Contract**: On parameter load, call `GetMonthPlanPreparationAsync` before loading the month. If the month exists, continue the normal `LoadAsync` path. If it does not exist and suggestions are available, store preparation state and render the suggestion UI without calling `GetMonthAsync`. If no suggestions exist or the user skips, create/load the month using the existing path.

#### 2. Historical Suggestion UI

**Files**:
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor`
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.cs`
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.css`

**Intent**: Let the user choose previous-year suggestions and modify planned amounts before creating the month.

**Contract**: Render a focused first-open panel listing source month, expense name, category/tag, source actual/planned amount, editable suggested planned amount, and selection checkbox. Provide primary action to apply selected rows and secondary action to skip suggestions. The copy should explain that active recurring expenses will still be added automatically when the month is created.

#### 3. Apply/Skip Handlers

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs`

**Intent**: Apply selected suggestions, create the month, and reload normal PlanPage state.

**Contract**: Apply selected suggestions through `ApplyMonthPlanSuggestionsAsync`, show success/empty/duplicate-aware snackbar messages, clear preparation state, refresh archive-month cache, and call `LoadAsync`. Skip must create/load the month with recurring auto-sync but without historical suggestions.

#### 4. Dirty-State Coverage

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.cs`

**Intent**: Avoid losing edited suggestion amounts if the user navigates away mid-preparation.

**Contract**: Include preparation suggestion selections and edited amounts in the dirty-state snapshot while the preparation panel is active. Reset dirty state after apply or skip.

#### 5. Target Month Copy UI

**Files**:
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor`
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs`

**Intent**: Extend existing copy mode to select a target month while preserving expense-only copy.

**Contract**: Keep current next-month default. Add compact target year/month controls visible in copy mode. Submit through `CopySelectedExpensesToMonthAsync`. Disable same-month target and closed target failures with clear snackbar feedback from the service.

#### 6. UI Contract Tests

**File**: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`

**Intent**: Guard the new first-open and copy UI contracts cheaply.

**Contract**: Add static source assertions for the preparation service call, apply/skip handlers, editable suggestion amounts, recurring-auto-sync explanatory copy, and absence of `Safe-to-spend` / `SafeToSpend`.

### Success Criteria

#### Automated Verification

- PlanPage UI contract tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- Targeted monthly-loop tests still pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual Verification

- Open a month that does not exist but has same-month-previous-year expenses; confirm suggestions appear before normal plan creation.
- Modify at least one suggested amount, apply selected suggestions, and confirm the created month contains active recurring items plus selected historical expenses with edited planned amounts and zero actuals.
- Open another missing month and skip suggestions; confirm the month still opens with recurring auto-sync and no historical expenses.
- Copy selected expenses to a non-adjacent target month and confirm no line items or actual amounts are copied.

**Implementation Note**: Use a targeted browser smoke here if static tests pass but the first-open interaction feels awkward in MudBlazor.

---

## Phase 3: Persisted Annual Plan Targets In Statistics

### Overview

Add a small year-level annual plan model for expected income and savings, then expose it in Statistics as `Plan roczny`.

### Changes Required

#### 1. Annual Plan Entity And Configuration

**Files**:
- `src/HouseholdBudgetMate.Domain/Entities/AnnualPlan.cs`
- `src/HouseholdBudgetMate.Domain/EntityConfiguration/AnnualPlanConfiguration.cs`
- `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs`

**Intent**: Persist one annual target row per budget owner and year.

**Contract**: `AnnualPlan` carries `UserId`, `Year`, `ExpectedIncomeAmount`, `ExpectedSavingsAmount`, timestamps, and an integer id. Configure required decimal fields, valid year index, and unique `(UserId, Year)` semantics through EF configuration and query filters.

#### 2. Migration

**Files**:
- `src/HouseholdBudgetMate.Migrations/Migrations/*_AddAnnualPlans.cs`
- `src/HouseholdBudgetMate.Migrations/Migrations/ApplicationDbContextModelSnapshot.cs`

**Intent**: Add the annual plans table without touching existing monthly data.

**Contract**: Migration creates `AnnualPlans` with user scope, year, expected income, expected savings, timestamps, foreign key to `Users`, and unique user/year index.

#### 3. Annual Plan Contracts

**Files**:
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/AnnualPlanDto.cs`
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Requests/UpsertAnnualPlanRequest.cs`
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/YearStatisticsDto.cs`
- `src/HouseholdBudgetMate.Abstractions/Interfaces/IExpenseService.cs`

**Intent**: Make annual targets available to Statistics and editable from the UI.

**Contract**: Add `AnnualPlan` to `YearStatisticsDto` and an `UpsertAnnualPlanAsync` service method. Request validates year and non-negative expected income/savings amounts.

#### 4. ExpenseService Annual Plan Handling

**File**: `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs`

**Intent**: Load and save annual targets through the same service Statistics already uses.

**Contract**: `GetYearStatisticsAsync` includes the annual plan for the selected year, defaulting to zero-valued DTO when absent. `UpsertAnnualPlanAsync` creates or updates the scoped row and returns the saved DTO.

#### 5. Statistics `Plan roczny` UI

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor`

**Intent**: Let users enter expected annual income and savings targets and compare them with current annual actuals.

**Contract**: Add a `Plan roczny` section near the annual summary. Include editable expected annual income and expected annual savings fields, save action, and read-only comparison against current income/savings actuals from existing yearly rows. Keep the UI dense and operational, not a landing-page/card-heavy redesign.

#### 6. Annual Plan Tests

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs`

**Intent**: Verify annual plan persistence, updates, user scoping, and statistics projection.

**Contract**: Add tests for default zero annual plan, upsert create/update, non-negative validation, inclusion in `GetYearStatisticsAsync`, and isolation between budget owners.

### Success Criteria

#### Automated Verification

- Annual plan service tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests"`
- Migration build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- Architecture tests still pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~Architecture"`

#### Manual Verification

- Open Statistics, edit `Plan roczny` income and savings targets, save, reload, and confirm values persist.
- Confirm monthly finance/annual actual tables still display existing actuals and are not reinterpreted as targets.

**Implementation Note**: Keep year-level totals only. Do not add monthly grids or category budgets in this phase.

---

## Phase 4: Deviation Alert Candidates

### Overview

Prepare category deviation alert candidates for future notifications without sending anything.

### Changes Required

#### 1. Alert Candidate DTO

**Files**:
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/CategoryDeviationAlertCandidateDto.cs`
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/YearStatisticsDto.cs`

**Intent**: Expose categories whose current spending is materially above historical average.

**Contract**: Candidate rows carry category id/name, compared month/year, current spent amount, historical average amount, percentage deviation, and threshold percentage. Add a `DeviationAlertCandidates` collection to `YearStatisticsDto`.

#### 2. Alert Candidate Calculation

**File**: `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs`

**Intent**: Build deterministic +20% category alert candidates from historical actuals.

**Contract**: For populated months in the selected year, compare each category's month spent amount against its historical average from prior populated months where available. Emit candidates when current spent amount exceeds average by more than 20%. No category exclusions are applied in this slice. Do not send events or notifications.

#### 3. Statistics Alert Foundation UI

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor`

**Intent**: Surface alert candidates as a transparent foundation for future notification work.

**Contract**: Add a concise Statistics section for categories above historical average. It should show month, category, current amount, average, and percentage above average. Copy must indicate these are candidates/prep, not sent notifications.

#### 4. Alert Candidate Tests

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs`

**Intent**: Guard alert candidate math without making notification behavior exist.

**Contract**: Test no candidate at exactly +20%, candidate over +20%, no candidate when insufficient prior history exists, and no notification/event side effect.

### Success Criteria

#### Automated Verification

- Alert candidate tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests"`
- Statistics UI contract tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"`

#### Manual Verification

- Open Statistics with data that produces a +20% candidate and confirm the alert-prep section is visible and clearly non-notification copy.

**Implementation Note**: If historical average semantics become ambiguous during implementation, prefer the simplest deterministic prior-populated-months average and document it in tests.

---

## Phase 5: Verification, Cookbook, And Evidence

### Overview

Run the risk-driven test layers, update local testing guidance for future monthly planning changes, and record manual smoke evidence.

### Changes Required

#### 1. Monthly Planning Test Cookbook Update

**File**: `context/foundation/test-plan.md`

**Intent**: Capture the reusable S-03 testing pattern.

**Contract**: Add or extend a cookbook note explaining that monthly preparation features should use service tests for suggestion/copy math, static UI contracts for first-open/copy/Statistics wiring, targeted monthly-loop tests for projection regressions, and browser smoke only for critical Blazor interaction checks.

#### 2. Acceptance Evidence

**File**: `context/changes/improve-monthly-planning/acceptance-evidence.md`

**Intent**: Record command results and manual verification for the S-03 flow.

**Contract**: Include targeted test commands, full test/build command results, first-open suggestion manual smoke notes, target-copy notes, `Plan roczny` persistence notes, and alert-prep notes.

#### 3. Full Verification

**Files**:
- `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs`
- `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs`
- `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`

**Intent**: Confirm S-03 did not regress the monthly-loop contract or revive superseded safe-to-spend wording.

**Contract**: Run targeted service/UI tests, full release test suite, release build, and whitespace check.

### Success Criteria

#### Automated Verification

- Targeted planning tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- Full release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`
- Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- Git whitespace check passes: `git diff --check -- .`

#### Manual Verification

- Browser smoke: missing month with previous-year expenses shows suggestions before month creation; edited price is applied after confirmation.
- Browser smoke: skipping suggestions creates/loads the month with recurring auto-sync and no historical suggestions.
- Browser smoke: copy selected expenses to a chosen non-adjacent target month.
- Browser smoke: `Plan roczny` target values persist after reload.
- Browser smoke: alert candidates appear as preparation only, with no sent-notification language.
- Review `acceptance-evidence.md` and confirm all command/manual results are recorded.

---

## Testing Strategy

### Unit Tests

- Suggested amount rounding helper, if extracted, should have focused tests for threshold and round-up behavior.
- Validation tests should cover invalid years/months, negative annual targets, empty selected suggestions, and same-month copy rejection.

### Integration Tests

- Extend `ExpenseServiceTests` for preparation preview, suggestion application, explicit-target copy, annual plan upsert/projection, and alert candidates.
- Extend `MonthlyBudgetingLoopTests` only if projection agreement changes after month preparation.
- Preserve existing `IncomeServiceTests` and live-balance contract; do not test annual targets through live balance.

### Static UI Contract Tests

- Extend `MonthlyBudgetingLoopUiTests` for PlanPage first-open suggestion wiring, target-copy controls, Statistics `Plan roczny`, alert-prep copy, and no `Safe-to-spend` wording.

### Manual Testing Steps

1. Seed or use data where July 2025 does not exist and July 2024 has expenses.
2. Open `/plan/2025/7`; confirm suggestion panel appears before the normal plan.
3. Edit one suggested amount, apply selected suggestions, and confirm the month rows.
4. Open another missing month and skip suggestions; confirm recurring auto-sync still happens.
5. Copy selected expenses to a chosen target month and confirm actuals/line items are not copied.
6. Save and reload `Plan roczny` in Statistics.
7. Verify alert candidates are informational and no notification is sent.

## Performance Considerations

Suggestion preview reads a single historical month plus active recurring definitions, so it should remain cheap for the small household data scale. Keep duplicate detection in memory over the small candidate set. Avoid scanning all history on PlanPage first open; broader three-month/category averages and alert candidates belong in Statistics/service projections, not the initial page load.

## Migration Notes

Phase 3 adds an `AnnualPlans` table. The migration should not backfill existing data. Existing users start with zero expected annual income and savings targets for each year until they save a `Plan roczny` value. Rollback removes only the annual targets table and does not affect existing month plans, expenses, incomes, or savings transfers.

## Rollback Notes

- Backend suggestion and copy contracts can be reverted without data migration if Phase 3 has not landed.
- After Phase 3, rollback must account for the `AnnualPlans` table migration.
- No changes should be made to existing recurring definitions or month plan rows except rows the user explicitly creates through apply/copy actions.

## References

- Research: `context/changes/improve-monthly-planning/research.md`
- Roadmap S-03: `context/foundation/roadmap.md:115`
- PRD month preparation guardrails: `context/foundation/prd.md:38`
- Existing copy implementation: `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1609`
- Existing copy UI: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:474`
- Existing statistics page: `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:284`
- Test cookbook: `context/foundation/test-plan.md:90`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` - <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Backend Month Preparation And Copy Contracts

#### Automated

- [x] 1.1 Targeted ExpenseService tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests"`
- [x] 1.2 Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual

- [ ] 1.3 Review service tests and confirm expected suggestion amounts are literal oracle values, not copied from production helper logic

### Phase 2: PlanPage First-Open Suggestions And Targeted Copy UX

#### Automated

- [x] 2.1 PlanPage UI contract tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- [x] 2.2 Targeted monthly-loop tests still pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- [x] 2.3 Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual

- [ ] 2.4 Open a missing month with same-month-previous-year expenses and confirm suggestions appear before normal plan creation
- [ ] 2.5 Modify one suggested amount, apply selected suggestions, and confirm the created month contains recurring items plus selected historical expenses with edited planned amounts and zero actuals
- [ ] 2.6 Open another missing month and skip suggestions; confirm the month opens with recurring auto-sync and no historical expenses
- [ ] 2.7 Copy selected expenses to a non-adjacent target month and confirm no line items or actual amounts are copied

### Phase 3: Persisted Annual Plan Targets In Statistics

#### Automated

- [x] 3.1 Annual plan service tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests"`
- [x] 3.2 Migration build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- [x] 3.3 Architecture tests still pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~Architecture"`

#### Manual

- [ ] 3.4 Open Statistics, edit `Plan roczny` income and savings targets, save, reload, and confirm values persist
- [ ] 3.5 Confirm monthly finance/annual actual tables still display existing actuals and are not reinterpreted as targets

### Phase 4: Deviation Alert Candidates

#### Automated

- [x] 4.1 Alert candidate tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests"`
- [x] 4.2 Statistics UI contract tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"`

#### Manual

- [ ] 4.3 Open Statistics with data that produces a +20% candidate and confirm the alert-prep section is visible and clearly non-notification copy

### Phase 5: Verification, Cookbook, And Evidence

#### Automated

- [x] 5.1 Targeted planning tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- [x] 5.2 Full release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`
- [x] 5.3 Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- [x] 5.4 Git whitespace check passes: `git diff --check -- .`

#### Manual

- [ ] 5.5 Browser smoke: missing month with previous-year expenses shows suggestions before month creation; edited price is applied after confirmation
- [ ] 5.6 Browser smoke: skipping suggestions creates/loads the month with recurring auto-sync and no historical suggestions
- [ ] 5.7 Browser smoke: copy selected expenses to a chosen non-adjacent target month
- [ ] 5.8 Browser smoke: `Plan roczny` target values persist after reload
- [ ] 5.9 Browser smoke: alert candidates appear as preparation only, with no sent-notification language
- [ ] 5.10 Review `acceptance-evidence.md` and confirm all command/manual results are recorded
