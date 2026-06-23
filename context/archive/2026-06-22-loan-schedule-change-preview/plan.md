# Loan Schedule Change Preview Implementation Plan

## Overview

Add a mandatory preview-and-confirm step before three operations that rebuild a loan schedule: applying a prepayment, adding a WIBOR rate entry, and applying a bank-provided installment amount/last-installment date. The preview shows the complete schedule before and after the proposed change, grouped into collapsible year sections, and commits only after explicit user confirmation.

The financial algorithms are a locked invariant. This change must not alter formulas, effective-date rules, principal allocation, interest calculation, charge calculation, period shortening, or rounding behavior. Preview and commit must share the existing calculation path so the preview cannot drift from the persisted result.

## Current State Analysis

The loans page already has separate user entry points for WIBOR, prepayment, and bank-provided schedule changes, but each entry point calls a mutating `ILoanService` method immediately. A successful mutation deletes/recreates affected installments, updates open month-plan expenses, and refreshes the selected loan. There is no read-only projection contract, no schedule version check between review and commit, and no common UI for comparing the current and proposed schedules.

The existing schedule UI is already componentized and can display hundreds of installments. The existing bank-update dialog explains impact but still writes on its primary action. The prepayment form is an inline `MudDialog`, while the WIBOR form lives in its tab. The parent `Loans.razor` owns mutation orchestration, form state, loading state, dirty-state resets, and refreshes.

### Key Discoveries

- `src/HouseholdBudgetMate.Application/Services/LoanService.cs:215` adds a WIBOR entry, rebuilds installments, saves, and synchronizes month plans in one operation.
- `src/HouseholdBudgetMate.Application/Services/LoanService.cs:270` applies prepayment directly and uses the existing `BuildSchedule` path for both reduce-installment and shorten-period strategies.
- `src/HouseholdBudgetMate.Application/Services/LoanService.cs:368` derives the implied principal for a bank installment, shortens the period, saves the rebuilt schedule, and records any implied prepayment.
- `src/HouseholdBudgetMate.Application/Services/LoanService.cs:932` and its existing helpers are the authoritative financial calculation implementation. Their formulas and rounding behavior are not in scope for modification.
- `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor:447`, `:742`, and `:786` currently submit WIBOR, bank-update, and prepayment changes immediately.
- `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanBankScheduleUpdateDialog.razor:1` provides an existing guided form and dirty-state pattern that should remain the input step.
- `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanScheduleTable.razor:1` establishes the current schedule columns and Polish money formatting.
- MudBlazor `MudExpansionPanel` does not keep collapsed content alive unless `KeepContentAlive` is enabled, so yearly panels can keep the DOM small while retaining the complete preview model.
- MudBlazor recommends table virtualization mainly for more than 1,000 rows. A typical 336-row mortgage schedule split by collapsed year panels does not need server paging or virtualization.

## Desired End State

For each schedule-changing operation, the user enters data and selects the current “apply” action. The application validates and calculates the proposal without saving, then opens a full-width comparison dialog. The dialog presents before/after summary metrics and all installments grouped by year. The first affected year is expanded; other years are collapsed and display annual summaries.

The user can return to the input form with all entered values intact or explicitly confirm the proposal. Confirmation succeeds only if the source loan schedule still matches the version used for preview. A stale preview is rejected with a clear message and must be recalculated. Canceling, closing, or previewing never changes loans, rate entries, installments, expenses, month plans, or audit history.

### Financial Invariants

- Existing `BuildSchedule`, `BuildVariableSchedule`, `BuildFixedSchedule`, `BuildMortgageFixedThenVariableSchedule`, installment-payment, principal-resolution, period-shortening, charge, and rounding rules remain behaviorally unchanged.
- Existing golden numeric assertions in `LoanServiceTests` remain unchanged and continue to define the expected financial results.
- The persisted result after confirmation must match the preview row-for-row and summary-for-summary for the same source version and request.
- A preview must not call `SaveChangesAsync`, create audit records, soft-delete expenses, synchronize month plans, or mutate tracked entities that escape the preview operation.

## What We're NOT Doing

- No changes to loan amortization formulas or rounding modes.
- No recalibration of expected principal, interest, installment, insurance, or charge values.
- No database schema changes, migrations, or persisted preview records.
- No preview for manual single-installment principal/interest override; “Wróć do edycji” means editing the input that produced a proposed schedule.
- No undo/rollback feature after a confirmed mutation.
- No changes to paid-installment rules, closed-month rules, audit semantics, or month-plan synchronization semantics.
- No charts, export, print layout, or bank-file import.
- No server-side pagination or virtualization for the yearly preview tables.

## Implementation Approach

Extract the state-independent parts of the three existing mutation workflows into internal projection routines inside the loan application service. Each routine receives the currently loaded loan plus the existing request contract and returns an internal projected schedule, projected end date, affected start date, and operation-specific metadata. The current write methods persist that projection and keep their existing side effects; new preview methods map the same projection to public comparison DTOs without saving.

Each preview returns an opaque source schedule version. The version represents all persisted inputs capable of changing the result: loan schedule configuration, end date, rate entries, active charges, installments and their paid state/amounts. Confirmation supplies the version with the original request. The service recomputes the source version before any mutation and throws `ConflictException` when it differs.

The web layer uses one shared comparison dialog for all three workflows. `Loans.razor` remains the state coordinator and preserves the relevant input state while the preview is open. The dialog receives only Abstractions DTOs, groups rows by year, and emits confirm or back-to-edit callbacks.

## Critical Implementation Details

### Shared Calculation Path

The implementer must not create a second amortization implementation for preview. Refactoring is limited to separating calculation from persistence around the current helpers. Existing numeric golden tests must pass without changing their expected values; any changed financial result is a regression, not an acceptable test update.

### State Sequencing

The required order is validate input, load the current loan snapshot, calculate projection, return preview, verify the opaque source version on confirmation, recalculate through the same projection routine, then persist and run existing synchronization side effects. Version comparison must happen before deleting installments, marking expenses deleted, adding rate entries, changing `EndDate`, or recording a prepayment expense.

### Preview Lifecycle

Closing the preview or choosing “Wróć do edycji” preserves input values. For prepayment and bank update it reopens the corresponding input dialog; for WIBOR it returns to the WIBOR tab with the fields intact. Only successful confirmation clears input state and marks the page dirty state pristine.

## Phase 1: Side-Effect-Free Schedule Projection Contracts

### Overview

Create the shared preview contracts and refactor the three application-service workflows so preview and commit use the same existing financial calculations. Add stale-preview protection without adding persistence.

### Changes Required

#### 1. Preview DTOs

**Files**: `src/HouseholdBudgetMate.Abstractions/Contracts/Loans/Dto/LoanScheduleChangePreviewDto.cs`, `LoanScheduleSummaryDto.cs`, `LoanScheduleComparisonRowDto.cs`

**Intent**: Define a presentation-neutral result that describes the proposal, before/after summaries, the first affected date, full before/after schedule rows, and an opaque source version.

**Contract**: The preview exposes loan id/name, change type/label, affected-from date, `SourceVersion`, summary values for remaining principal, next installment, total future interest, end date and installment count, plus comparison rows keyed by due date. Rows support unchanged, changed, added, and removed states so shorten-period tails remain representable.

#### 2. Request Version Contract

**Files**: `src/HouseholdBudgetMate.Abstractions/Contracts/Loans/Requests/AddLoanRateEntryRequest.cs`, `ApplyLoanPrepaymentRequest.cs`, `ApplyLoanInstallmentAmountChangeRequest.cs`

**Intent**: Carry the source version reviewed by the user into the existing write operation.

**Contract**: Add an optional `ExpectedScheduleVersion` field used only during confirmation. Preview methods accept requests without it; the three write methods require it for user-triggered schedule mutations and reject missing or stale versions before mutation.

#### 3. Loan Service Interface

**File**: `src/HouseholdBudgetMate.Abstractions/Interfaces/ILoanService.cs`

**Intent**: Add explicit read-only preview operations for WIBOR, prepayment, and bank schedule changes while retaining the existing write method names and result DTOs.

**Contract**: Add one asynchronous preview method per existing mutation request, each returning `LoanScheduleChangePreviewDto` and accepting a `CancellationToken`.

#### 4. Shared Projection Engine

**File**: `src/HouseholdBudgetMate.Application/Services/LoanService.cs` and focused partial/helper files if extraction is needed

**Intent**: Separate calculation from persistence around the existing schedule helpers so the preview and confirmed write cannot diverge.

**Contract**: Internal projection routines reuse the current schedule builders, validation rules, affected-installment selection, principal derivation, end-date resolution, and charge rules. They do not attach proposed entities to the DbContext and do not modify the loaded entity graph during preview.

#### 5. Source Version Guard

**File**: `src/HouseholdBudgetMate.Application/Services/LoanService.cs`

**Intent**: Prevent confirmation of a preview calculated from obsolete loan data.

**Contract**: Generate a deterministic opaque version from calculation-relevant persisted state. Recompute and compare it immediately before the confirmed mutation; mismatch raises `ConflictException` and performs no writes.

#### 6. Test Double Compatibility

**File**: `src/HouseholdBudgetMate.Tests/Shared/NoOpLoanService.cs`

**Intent**: Keep UI test doubles aligned with the expanded service contract.

**Contract**: Preview methods return deterministic empty preview DTOs suitable for non-loan UI tests.

### Success Criteria

#### Automated Verification

- Existing financial golden tests pass with no expected numeric values changed.
- Preview tests for all three operations prove database state, audit rows, expenses, month plans, rate entries and installments are unchanged.
- For each operation, preview rows and summary equal the result returned after confirmed persistence from the same source version.
- A missing or stale source version rejects confirmation before any database mutation.
- `dotnet build HouseholdBudgetMate.slnx` and focused `LoanServiceTests` pass.

#### Manual Verification

- None; this phase exposes no user-visible UI.

---

## Phase 2: Shared Year-Grouped Comparison Dialog

### Overview

Build a reusable MudBlazor dialog that presents the full before/after schedule without overwhelming the page or rendering every year’s rows at once.

### Changes Required

#### 1. Preview Dialog

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanScheduleChangePreviewDialog.razor`

**Intent**: Provide one consistent confirmation surface for all schedule-changing operations.

**Contract**: The dialog accepts `LoanScheduleChangePreviewDto`, loading/confirming state, and callbacks for confirm and back-to-edit. It uses a wide responsive `DialogOptions`, a bounded scrollable content area, clear operation title, and explicit “Wróć do edycji” and “Potwierdź i zapisz” actions.

#### 2. Before/After Summary

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanSchedulePreviewSummary.razor`

**Intent**: Show the most important consequences before the detailed rows.

**Contract**: Compare remaining principal, next installment, total future interest, end date and installment count. Values use `pl-PL` formatting and show both old/new values plus delta where meaningful.

#### 3. Year Panels

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanSchedulePreviewYearPanel.razor`

**Intent**: Keep a decades-long schedule navigable and prevent the modal layout from expanding uncontrollably.

**Contract**: Group the complete schedule by year. The first affected year starts expanded; all other years start collapsed. Each header shows year, row count and annual before/after totals. Keep collapsed content out of the DOM (`KeepContentAlive=false`) and allow users to expand additional years.

#### 4. Comparison Table

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanSchedulePreviewTable.razor`

**Intent**: Make row-level changes auditable without changing the existing schedule table.

**Contract**: Show month, total installment, principal, interest and costs with before/after values. Support horizontal scrolling on narrow screens, aligned financial columns, added/removed row labels, paid-row context, and accessible table/dialog labels.

#### 5. Scoped Styling

**Files**: colocated `.razor.css` files or `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor.css`

**Intent**: Keep the dialog stable on desktop and mobile without adding global layout rules.

**Contract**: Bound dialog height, sticky/visible action area, responsive KPI grid, horizontal table scrolling, delta emphasis, muted unchanged context, and dark-mode-compatible colors.

### Success Criteria

#### Automated Verification

- UI contract tests verify summary fields, year grouping, first-affected-year expansion, back and confirm actions, and before/after columns.
- `dotnet build HouseholdBudgetMate.slnx` passes without new Razor analyzer errors.

#### Manual Verification

- A 336-installment schedule opens without page-width or dialog-height breakage.
- Only the first affected year is initially expanded; other years can be expanded independently.
- Desktop, narrow mobile and dark mode remain readable, with action buttons always reachable.
- Added/removed installments from shortened periods are understandable in the comparison.

**Implementation Note**: Pause for manual layout confirmation before wiring live mutations, because yearly expansion and table density are the primary UX risk.

---

## Phase 3: Two-Step Integration for WIBOR, Prepayment and Bank Update

### Overview

Replace immediate writes with preview orchestration while preserving all input values, dirty-state behavior and existing success side effects.

### Changes Required

#### 1. Parent Workflow State

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor`

**Intent**: Coordinate the active preview, its originating operation, original request, source version, loading state, and return-to-edit behavior.

**Contract**: Maintain one preview at a time. Disable duplicate preview/confirm submissions. Clear preview/request state only after success or explicit discard; retain it when returning to edit or after stale-version rejection.

#### 2. WIBOR Preview Flow

**Files**: `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor`, `LoansPage/LoanWiborPanel.razor`

**Intent**: Turn “Aktualizuj WIBOR” into a read-only calculation followed by explicit confirmation.

**Contract**: Valid input calls the WIBOR preview method. Back returns to the WIBOR tab with date/rate intact. Confirm supplies `ExpectedScheduleVersion`, calls the existing `AddLoanRateEntryAsync`, then clears fields, reloads loans and shows the existing success notification.

#### 3. Prepayment Preview Flow

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor`

**Intent**: Insert comparison between the current prepayment form and the existing write call for both reduce-installment and shorten-period strategies.

**Contract**: Preview preserves target installment, amount and strategy. Back reopens the prepayment dialog with those values. Confirm calls `ApplyLoanPrepaymentAsync` with the reviewed source version and retains all current post-save refresh and notification behavior.

#### 4. Bank Schedule Preview Flow

**Files**: `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor`, `LoansPage/LoanBankScheduleUpdateDialog.razor`

**Intent**: Preview the bank-provided installment amount and shortened end date before changing the schedule or recording the implied prepayment.

**Contract**: Back reopens the bank dialog with amount and date intact. Confirm calls `ApplyLoanInstallmentAmountChangeAsync` with the reviewed source version and preserves current synchronization and notification behavior.

#### 5. Conflict and Error UX

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor`

**Intent**: Make stale or invalid proposals recoverable without losing user input.

**Contract**: `ConflictException` or its surfaced message closes/disables the obsolete preview, preserves inputs, and tells the user to recalculate. Validation or preview errors remain on the input step. Failed confirmation performs no optimistic UI update.

#### 6. Dirty-State Integration

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor`

**Intent**: Prevent navigation from silently discarding a prepared proposal while avoiding false dirty state after successful confirmation.

**Contract**: Existing input snapshots remain authoritative while preview is open. Back preserves them; cancel/discard and successful save reset them through the existing dirty-state mechanism.

### Success Criteria

#### Automated Verification

- UI tests prove all three actions call preview before their write methods.
- UI tests prove back-to-edit retains each operation’s inputs and successful confirmation clears them.
- UI tests prove stale-preview errors preserve input and do not show a success message.
- Existing loan UI redesign tests continue to pass.
- `dotnet build HouseholdBudgetMate.slnx` and focused UI/service tests pass.

#### Manual Verification

- WIBOR follows: enter values, preview, inspect years, return/edit, preview again, confirm.
- Prepayment follows the same flow for both strategies and displays the expected changed schedule.
- Bank update follows the same flow and clearly shows removed tail installments when the period shortens.
- Closing or canceling at every step leaves persisted data unchanged.

**Implementation Note**: Pause for manual confirmation of all three workflows before final regression verification.

---

## Phase 4: Financial Regression and Acceptance Verification

### Overview

Prove that the feature changes workflow and presentation only, not any financial result, and capture repeatable acceptance evidence for a real long mortgage schedule.

### Changes Required

#### 1. Service Regression Matrix

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/LoanServiceTests.cs`

**Intent**: Protect numerical parity between current behavior, preview and confirmed persistence.

**Contract**: Cover variable WIBOR change, prepayment/reduce installment, prepayment/shorten period, percentage insurance, bank installment change, paid installments, open future month plans, and end-of-schedule rounding. Reuse existing expected numbers unchanged.

#### 2. UI Workflow Coverage

**Files**: `src/HouseholdBudgetMate.Tests/Tests/Ui/LoanUiRedesignTests.cs` and rendered component tests where practical

**Intent**: Protect the presence and sequencing of preview, yearly comparison, confirmation, back-to-edit and stale-state handling.

**Contract**: Assertions focus on user-visible behavior and service-call ordering rather than private field names where rendered tests are feasible.

#### 3. Acceptance Evidence

**File**: `context/changes/loan-schedule-change-preview/acceptance-evidence.md`

**Intent**: Record automated results and the manual walkthrough of a representative mortgage.

**Contract**: Include the 800,000 PLN mortgage scenario, WIBOR 3.80 to 3.73, a prepayment under both strategies, bank shortening, full year-panel navigation, stale-preview rejection and confirmation that historic/paid rows remain unchanged.

### Success Criteria

#### Automated Verification

- All existing financial expected values remain unchanged in source control.
- Preview and persisted schedules are identical for the full regression matrix.
- `dotnet test HouseholdBudgetMate.slnx -c Release` passes.
- `dotnet build HouseholdBudgetMate.slnx -c Release` passes.
- `git diff --check` passes.

#### Manual Verification

- Representative long-mortgage previews match the existing persisted results for all three operations.
- No preview action creates financial data, audit history or month-plan changes.
- Year sections, summaries and tables remain usable across desktop/mobile and dark mode.
- Human reviewer confirms the financial algorithm and expected numeric fixtures were not changed.

## Testing Strategy

### Unit and Service Tests

- Preview validation matches the corresponding write validation for all request fields except the confirmation version.
- Preview does not change entity counts, values, soft-delete flags, audit logs or month plans.
- Preview summary totals reconcile with its rows.
- Preview and confirmed write produce identical due dates, amounts, principal, interest, costs, end date and installment count.
- Stale source version rejects all three write operations before mutation.
- Shorten-period previews represent removed rows and do not fabricate zero-value persisted installments.
- Existing rounding-edge and real mortgage expected-value tests stay unchanged.

### UI Tests

- Correct operation label and summary are rendered.
- Full schedule is grouped by year and the first affected year is expanded.
- Before/after values and deltas use Polish money formatting.
- Back-to-edit retains input values for all three operations.
- Confirm is disabled during submission and cannot double-submit.
- Stale preview shows a recoverable warning and requires recalculation.

### Manual Testing Steps

1. Open a variable-rate mortgage with a multi-decade schedule and record current schedule values.
2. Enter a new WIBOR value, preview it, inspect first/middle/final years, return to edit, change the value, preview again and confirm.
3. Compare the confirmed schedule with the last preview and verify exact row parity.
4. Repeat for prepayment with `ReduceInstallment` and `ShortenPeriod`.
5. Repeat for the bank-provided installment amount and end-date workflow.
6. Open a preview in one session, mutate the schedule from another action/session, then verify confirmation is rejected as stale.
7. Cancel each operation from both the input and preview stages and verify database/audit state remains unchanged.
8. Repeat the dialog walkthrough on narrow viewport and dark mode.

## Performance Considerations

- Calculate the full preview once per explicit user action; do not recalculate on every expansion or render.
- Keep all year comparison models in memory for the dialog, but render collapsed panel content lazily by leaving `KeepContentAlive` disabled.
- Do not enable table virtualization by default for the expected approximately 336 rows; yearly panels already limit DOM size, and MudBlazor positions virtualization primarily for datasets over 1,000 rows.
- Disable duplicate preview and confirmation calls and honor cancellation tokens in database reads.
- Do not cache previews across source-version changes; correctness is more important than avoiding one recalculation.

## Migration Notes

No database migration or data backfill is required. Preview state and source versions are transient. Rollback consists of reverting the preview contracts/UI and restoring direct write calls; persisted loan data remains compatible because write-side financial behavior and schema do not change.

## References

- Existing write workflows: `src/HouseholdBudgetMate.Application/Services/LoanService.cs:215`, `:270`, `:368`
- Existing page orchestration: `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor:447`, `:742`, `:786`
- Existing bank input dialog: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanBankScheduleUpdateDialog.razor:1`
- Existing schedule table: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanScheduleTable.razor:1`
- Existing financial regression tests: `src/HouseholdBudgetMate.Tests/Tests/Services/LoanServiceTests.cs:471`, `:746`, `:890`, `:993`
- Prior UI redesign: `context/archive/2026-06-16-loan-ui-ux-redesign/plan.md`
- MudExpansionPanel API: `https://mudblazor.com/api/MudExpansionPanel`
- MudTable API: `https://mudblazor.com/api/MudTable%601`
- MudDialog guidance: `https://mudblazor.com/components/dialog`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Side-Effect-Free Schedule Projection Contracts

#### Automated

- [x] 1.1 Existing financial golden tests pass with no expected numeric changes
- [x] 1.2 Preview operations are side-effect free for all three schedule changes
- [x] 1.3 Preview and confirmed persistence produce identical schedules
- [x] 1.4 Missing or stale source versions reject writes before mutation
- [x] 1.5 Solution build and focused LoanService tests pass

### Phase 2: Shared Year-Grouped Comparison Dialog

#### Automated

- [x] 2.1 UI contract tests cover summary, year grouping and comparison actions
- [x] 2.2 Solution build passes without new Razor analyzer errors

#### Manual

- [ ] 2.3 Long schedule dialog remains stable and navigable
- [ ] 2.4 Year expansion behavior matches the approved UX
- [ ] 2.5 Desktop, mobile and dark mode remain readable
- [ ] 2.6 Shortened-period added and removed rows are understandable

### Phase 3: Two-Step Integration for WIBOR, Prepayment and Bank Update

#### Automated

- [x] 3.1 All three UI actions preview before writing
- [x] 3.2 Back-to-edit retains inputs and confirmation clears them
- [x] 3.3 Stale-preview errors preserve inputs and suppress success state
- [x] 3.4 Existing loan UI tests and focused build/tests pass

#### Manual

- [ ] 3.5 WIBOR preview-edit-confirm flow works end to end
- [ ] 3.6 Both prepayment strategies work through preview and confirmation
- [ ] 3.7 Bank schedule shortening clearly represents removed installments
- [ ] 3.8 Canceling every stage leaves persisted data unchanged

### Phase 4: Financial Regression and Acceptance Verification

#### Automated

- [x] 4.1 Existing expected financial values remain unchanged
- [x] 4.2 Full preview/persistence regression matrix is identical
- [x] 4.3 Release test suite passes
- [x] 4.4 Release build passes
- [x] 4.5 Git diff check passes

#### Manual

- [ ] 4.6 Representative long-mortgage previews match persisted results
- [ ] 4.7 Preview creates no financial or audit side effects
- [ ] 4.8 Responsive yearly schedule review is usable
- [ ] 4.9 Human reviewer confirms financial algorithms and fixtures were unchanged
