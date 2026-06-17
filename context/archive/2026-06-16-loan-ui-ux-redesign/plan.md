# Loan UI/UX Redesign Implementation Plan

## Overview

Redesign the loan management UI into a focused servicing workspace for active loans. The change keeps backend services, DTOs, persistence, and loan algorithms unchanged while improving the way users inspect schedules, edit loan metadata, update WIBOR, manage costs, and apply bank-provided installment updates.

## Current State Analysis

`src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor` currently combines nearly all loan UI responsibilities in one page: create form, loan list, edit form, WIBOR form/history, cost management, schedule table, and three dialogs. This works functionally, but the page makes setup, servicing, and schedule operations compete at the same visual priority.

The current schedule table is the right base pattern because loan installments are multivariate data across time. External UX guidance supports keeping a table for comparison-heavy data, but adding discoverable filters, clear column grouping, and restrained row actions.

### Key Discoveries:

- `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor:34` starts with the full "New loan" form, making creation dominate even when the user mainly manages an existing mortgage.
- `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor:168` shows loans in a dense table with simple actions.
- `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor:205` mixes edit, WIBOR, rate history, and costs in one expanded editing surface.
- `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor:411` has the core installment schedule table, but each row carries too many visible actions.
- `src/HouseholdBudgetMate.Web/wwwroot/app.css:40` and related styles establish the app's existing panel/KPI vocabulary.
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.css:314` shows an existing pattern for scrollable dense financial tables.
- Carbon Design System recommends table toolbars for search, filtering, settings, and global table actions, while row overflow menus are appropriate when row actions exceed a few options.
- Nielsen Norman Group identifies the core data-table tasks as finding records, comparing data, viewing/editing rows, and taking actions on records.

## Desired End State

The loans page defaults to an active-loan servicing experience. Users choose a loan, see a concise financial summary, move through tabs for schedule/WIBOR/costs/settings, and work with a filtered schedule table whose common action is obvious and whose advanced actions are available without clutter.

The frontend code is split into focused components while preserving existing service calls and request contracts.

## What We're NOT Doing

- No backend service contract changes.
- No database schema or migration changes.
- No changes to loan amortization, WIBOR, prepayment, or bank installment amount algorithms.
- No new external UI framework.
- No new charting or advanced projection engine.
- No changes to recurring/month plan synchronization behavior except preserving existing user actions.

## Implementation Approach

Keep `Loans.razor` as the routed page and state coordinator, but extract loan UI surfaces into child components. The parent component remains responsible for loading data, calling `ILoanService` and `ICategoryService`, managing dialogs where needed, and refreshing state after mutations. Child components receive DTOs, local view state, and callbacks for existing operations.

Use page-specific CSS for the redesign, preferably `Loans.razor.css`, so loan-specific layout rules do not keep expanding global `app.css`.

## Phase 1: Component Baseline

### Overview

Split the current page into focused frontend components without changing behavior or visual hierarchy yet. This reduces risk before redesigning the experience.

### Changes Required:

#### 1. Loan Component Folder

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/*`

**Intent**: Create a home for loan-specific UI components so `Loans.razor` can become an orchestrator instead of a monolith.

**Contract**: Components should use existing `LoanDto`, `LoanInstallmentDto`, `LoanChargeDto`, request models, callbacks, and MudBlazor primitives. No new abstraction project or backend dependency.

#### 2. Loan List Component

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanListPanel.razor`

**Intent**: Move the existing loan list table into a component that displays loans and raises callbacks for selecting schedule, editing, and deleting.

**Contract**: Inputs include loans, selected loan id, culture/formatting support if needed, and callbacks for current actions.

#### 3. Loan Create Component

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanCreatePanel.razor`

**Intent**: Isolate the existing create form so it can later be moved behind a dialog or collapsible creation area.

**Contract**: Preserve current create model fields, category tag selection behavior, date handlers, and submit callback.

#### 4. Loan Schedule Component

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanScheduleTable.razor`

**Intent**: Move the existing schedule table into a component before adding filters and action-menu behavior.

**Contract**: Preserve existing actions: toggle paid, prepay, bank installment amount change, sync to month, and edit installment.

### Success Criteria:

#### Automated Verification:

- `dotnet build HouseholdBudgetMate.slnx` passes.
- `dotnet test HouseholdBudgetMate.slnx` passes or any failures are unrelated and documented.

#### Manual Verification:

- The loans page still loads.
- Existing create/edit/schedule/dialog actions remain available.
- No visible regression is introduced before redesign work begins.

**Implementation Note**: After this phase, pause for a quick manual smoke test because behavior-preserving extraction is where silent callback regressions are easiest to introduce.

---

## Phase 2: Active Loan Workspace

### Overview

Make the selected loan the center of the page and reduce the default dominance of the create form.

### Changes Required:

#### 1. Workspace Layout

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor`

**Intent**: Reframe the page around a selected-loan workspace: compact page header, loan selector/list, and selected-loan detail area.

**Contract**: Keep route `/loans`, existing injected services, dirty-state monitoring, and current load/refresh behavior.

#### 2. Loan Summary Component

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanSummaryHeader.razor`

**Intent**: Add a concise KPI header for the selected loan: remaining principal, next unpaid installment, current rate label, end date, and paid/remaining installment count.

**Contract**: Use only fields already available on `LoanDto` and `LoanInstallmentDto`. If a value cannot be derived from the DTO, omit it rather than changing the backend.

#### 3. Add Loan Entry Point

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor`

**Intent**: Move "New loan" behind a clear primary action, collapsible panel, or dialog so servicing an existing loan is the default screen experience.

**Contract**: The create form still submits through the existing `CreateLoanAsync` flow.

### Success Criteria:

#### Automated Verification:

- `dotnet build HouseholdBudgetMate.slnx` passes.

#### Manual Verification:

- A user can immediately identify the selected loan and its key numbers.
- Adding a new loan remains discoverable.
- Desktop layout does not require scanning past the full create form before reaching the active loan.

---

## Phase 3: Tabbed Servicing

### Overview

Organize selected-loan work into tabs: Summary, Schedule, WIBOR, Costs, and Settings.

### Changes Required:

#### 1. Loan Tabs Component

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanWorkspaceTabs.razor`

**Intent**: Provide the top-level tab navigation for the selected loan.

**Contract**: Tabs should map to existing operations only. They must not hide dirty-state warnings or make unsaved form state ambiguous.

#### 2. WIBOR Panel

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanWiborPanel.razor`

**Intent**: Move the WIBOR update form and rate-entry table into a dedicated tab.

**Contract**: Preserve `AddRateEntryAsync` behavior and existing rate-entry display ordering.

#### 3. Costs Panel

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanCostsPanel.razor`

**Intent**: Move loan charge creation, list, activation, and deletion into a dedicated tab.

**Contract**: Preserve all existing charge actions and validations.

#### 4. Settings Panel

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanSettingsPanel.razor`

**Intent**: Move loan metadata and schedule settings into a dedicated tab.

**Contract**: Preserve the existing update request and paid-installment constraints surfaced by the service.

### Success Criteria:

#### Automated Verification:

- `dotnet build HouseholdBudgetMate.slnx` passes.

#### Manual Verification:

- Users can switch tabs without losing selected loan context.
- Dirty-state behavior remains understandable when editing settings or dialog fields.
- WIBOR and costs no longer visually compete with the schedule table.

---

## Phase 4: Schedule Table UX

### Overview

Improve the installment schedule for scanning, filtering, and repeated actions.

### Changes Required:

#### 1. Schedule Toolbar

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanScheduleToolbar.razor`

**Intent**: Add table controls for year, status, upcoming/unpaid view, and reset filters.

**Contract**: Filtering is client-side over the currently loaded `LoanDto.Installments`; no service/API changes.

#### 2. Schedule Row Presentation

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanScheduleTable.razor`

**Intent**: Reorder and polish columns for fast comparison: due date, total, principal, interest, costs, status, actions. Highlight the next unpaid installment and keep numeric columns visually aligned.

**Contract**: Existing installment amounts and derived costs remain unchanged.

#### 3. Row Action Menu

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanScheduleRowActions.razor`

**Intent**: Keep the primary paid/unpaid action visible and move advanced row actions into an overflow menu.

**Contract**: Expose callbacks for the same actions currently present in each row.

### Success Criteria:

#### Automated Verification:

- `dotnet build HouseholdBudgetMate.slnx` passes.

#### Manual Verification:

- User can filter to unpaid/future installments.
- User can still prepay, update bank installment amount, sync to month, and edit a row.
- The table remains readable with hundreds of installments.
- Mobile layout does not create overlapping action buttons or unreadable amounts.

---

## Phase 5: Bank Schedule Update Workflow

### Overview

Make the "change installment from bank" workflow explicit and safer without changing the backend.

### Changes Required:

#### 1. Bank Update Dialog/Panel

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanBankScheduleUpdateDialog.razor`

**Intent**: Replace the minimal dialog with a guided UI that explains the affected start installment, accepted installment amount, last installment date, and the fact that future unpaid installments will be rebuilt.

**Contract**: Continue calling `ApplyLoanInstallmentAmountChangeAsync` with `LoanInstallmentId`, `InstallmentAmount`, and `LastInstallmentDate`.

#### 2. Workflow Entry Points

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanScheduleTable.razor`

**Intent**: Allow the workflow from the selected row action menu and, optionally, from a schedule-level toolbar action that defaults to the next unpaid installment.

**Contract**: If a toolbar entry point is added, it must still resolve to an existing installment id before calling the service.

#### 3. Confirmation Copy

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanBankScheduleUpdateDialog.razor`

**Intent**: Use plain language for impact: "Zmienimy przyszłe niezapłacone raty od tej daty na podstawie kwoty z banku i daty ostatniej raty."

**Contract**: Copy must not claim previewed algorithm results unless the UI can derive them from existing returned data after save.

### Success Criteria:

#### Automated Verification:

- `dotnet build HouseholdBudgetMate.slnx` passes.

#### Manual Verification:

- User understands this is a schedule-changing workflow, not a simple label edit.
- The workflow works for the real scenario: loan 800000, WIBOR 3.8, later WIBOR 3.73, then bank-provided installment amount and last installment date.
- Canceling the dialog leaves no dirty state behind.

---

## Phase 6: Polish & Responsive QA

### Overview

Add page-specific styling and verify the redesigned UI is stable across desktop, tablet, mobile, and dark mode.

### Changes Required:

#### 1. Page Styles

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor.css`

**Intent**: Define loan-page layout, KPI grid, tabs spacing, schedule table density, toolbar wrapping, and mobile fallbacks.

**Contract**: Avoid broad global selectors. Prefer classes scoped to the loans page and keep global `app.css` changes minimal.

#### 2. Empty and Edge States

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/*`

**Intent**: Add polished states for no loans, no installments after filters, no charges, no WIBOR history, and no selected loan.

**Contract**: Empty states should be informational and should route users to existing actions.

#### 3. Accessibility and Interaction Details

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/*`

**Intent**: Ensure controls have understandable labels, icon-only actions have accessible names/tooltips, focus order is sensible, and text fits inside controls.

**Contract**: Use MudBlazor components and existing app conventions.

### Success Criteria:

#### Automated Verification:

- `dotnet build HouseholdBudgetMate.slnx` passes.
- `dotnet test HouseholdBudgetMate.slnx` passes or any failures are unrelated and documented.

#### Manual Verification:

- Desktop and mobile layouts have no overlapping text or controls.
- Dark mode remains readable.
- Long loan names and large PLN values do not break the layout.

---

## Phase 7: Verification

### Overview

Verify the full redesigned flow through automated checks and manual scenarios.

### Changes Required:

#### 1. Render/UI Contract Tests

**File**: `src/HouseholdBudgetMate.Tests/Tests/Ui/*`

**Intent**: Add lightweight tests for key labels and workflows if the existing test infrastructure supports rendering the loans page with service stubs.

**Contract**: Tests should protect user-visible behavior, not the internal component structure.

#### 2. Manual Scenario Evidence

**File**: `context/changes/loan-ui-ux-redesign/acceptance-evidence.md`

**Intent**: Record the manual walkthrough results for the main loan scenarios.

**Contract**: Include at least add/select loan, schedule filtering, mark paid/unpaid, prepayment dialog, bank update dialog, WIBOR tab, costs tab, settings tab, desktop/mobile smoke.

### Success Criteria:

#### Automated Verification:

- `dotnet build HouseholdBudgetMate.slnx` passes.
- `dotnet test HouseholdBudgetMate.slnx` passes.
- Any added UI tests pass.

#### Manual Verification:

- Real mortgage-like workflow is verified: start 800000, initial WIBOR 3.8, change to 3.73, then update from bank installment amount and last installment date.
- Existing loan actions remain available after redesign.
- User can explain where to go for schedule, WIBOR, costs, and settings without reading helper text.

---

## Testing Strategy

### Unit Tests:

- Do not add backend unit tests unless frontend extraction reveals an existing service behavior regression.
- Keep existing `LoanServiceTests` untouched unless a UI test requires stable seeded examples.

### Integration/UI Tests:

- Prefer lightweight rendered UI tests for presence of major page regions: loan list, selected loan summary, schedule tab, WIBOR tab, costs tab, settings tab.
- Test that advanced row actions are still represented in the schedule row action menu.

### Manual Testing Steps:

1. Open `/loans` with no loans and confirm the empty state points to adding a loan.
2. Add a mortgage and confirm it becomes selectable.
3. Select an active loan and inspect KPI values.
4. Switch between tabs and confirm the selected loan remains stable.
5. Filter the schedule to unpaid/future installments.
6. Mark a payment paid and unpaid.
7. Open prepayment, bank installment amount change, and edit installment workflows.
8. Add a WIBOR entry and verify schedule refresh behavior still works.
9. Add, deactivate, reactivate, and delete a loan cost.
10. Check desktop, tablet, mobile, and dark mode.

## Performance Considerations

Filtering should be client-side over the already loaded installment list. Avoid expensive recalculation inside row rendering; derive filtered lists once per render path or through simple computed collections.

The schedule may contain hundreds of rows, so the table needs stable column widths, horizontal overflow on smaller screens, and no large nested UI per row.

## Migration Notes

No data migration is required. The change is frontend-only and should be deployable/revertible independently from loan algorithm work.

## References

- Current page: `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor`
- Existing global style vocabulary: `src/HouseholdBudgetMate.Web/wwwroot/app.css`
- Existing dense table styling pattern: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.css`
- Carbon Design System data table guidance: https://carbondesignsystem.com/components/data-table/usage/
- Nielsen Norman Group data table guidance: https://www.nngroup.com/articles/data-tables/
- U.S. Web Design System table guidance: https://designsystem.digital.gov/components/table/

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append `-- <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Component Baseline

#### Automated

- [x] 1.1 `dotnet build HouseholdBudgetMate.slnx` passes
- [x] 1.2 `dotnet test HouseholdBudgetMate.slnx` passes or unrelated failures are documented

#### Manual

- [ ] 1.3 The loans page still loads
- [ ] 1.4 Existing create/edit/schedule/dialog actions remain available
- [ ] 1.5 No visible regression is introduced before redesign work begins

### Phase 2: Active Loan Workspace

#### Automated

- [x] 2.1 `dotnet build HouseholdBudgetMate.slnx` passes

#### Manual

- [ ] 2.2 A user can immediately identify the selected loan and its key numbers
- [ ] 2.3 Adding a new loan remains discoverable
- [ ] 2.4 Desktop layout reaches the active loan without scanning past the full create form

### Phase 3: Tabbed Servicing

#### Automated

- [x] 3.1 `dotnet build HouseholdBudgetMate.slnx` passes

#### Manual

- [ ] 3.2 Users can switch tabs without losing selected loan context
- [ ] 3.3 Dirty-state behavior remains understandable
- [ ] 3.4 WIBOR and costs no longer visually compete with the schedule table

### Phase 4: Schedule Table UX

#### Automated

- [x] 4.1 `dotnet build HouseholdBudgetMate.slnx` passes

#### Manual

- [x] 4.2 User can filter to unpaid/future installments
- [x] 4.3 User can still prepay, update bank installment amount, sync to month, and edit a row
- [x] 4.4 The table remains readable with hundreds of installments
- [x] 4.5 Mobile layout does not create overlapping action buttons or unreadable amounts

### Phase 5: Bank Schedule Update Workflow

#### Automated

- [x] 5.1 `dotnet build HouseholdBudgetMate.slnx` passes

#### Manual

- [ ] 5.2 User understands this is a schedule-changing workflow
- [ ] 5.3 The real 800000 / WIBOR 3.8 / WIBOR 3.73 / bank installment update scenario works
- [ ] 5.4 Canceling the dialog leaves no dirty state behind

### Phase 6: Polish & Responsive QA

#### Automated

- [x] 6.1 `dotnet build HouseholdBudgetMate.slnx` passes
- [x] 6.2 `dotnet test HouseholdBudgetMate.slnx` passes or unrelated failures are documented

#### Manual

- [ ] 6.3 Desktop and mobile layouts have no overlapping text or controls
- [ ] 6.4 Dark mode remains readable
- [ ] 6.5 Long loan names and large PLN values do not break the layout

### Phase 7: Verification

#### Automated

- [x] 7.1 `dotnet build HouseholdBudgetMate.slnx` passes
- [x] 7.2 `dotnet test HouseholdBudgetMate.slnx` passes
- [x] 7.3 Any added UI tests pass

#### Manual

- [ ] 7.4 Real mortgage-like workflow is verified
- [ ] 7.5 Existing loan actions remain available after redesign
- [ ] 7.6 User can explain where to go for schedule, WIBOR, costs, and settings without reading helper text
