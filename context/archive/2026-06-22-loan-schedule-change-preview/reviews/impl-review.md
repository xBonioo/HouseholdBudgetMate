<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Loan Schedule Change Preview

- **Plan**: `context/changes/loan-schedule-change-preview/plan.md`
- **Scope**: Phases 1-4
- **Date**: 2026-06-22
- **Verdict**: APPROVED
- **Findings**: 0 critical, 0 unresolved warnings, 0 unresolved observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 - Preview and persistence used separate calculation orchestration

- **Severity**: WARNING
- **Impact**: HIGH
- **Dimension**: Plan Adherence / Architecture
- **Location**: `src/HouseholdBudgetMate.Application/Services/LoanService.cs`
- **Detail**: Preview and write methods duplicated affected-installment selection, principal derivation, end-date resolution, and schedule construction.
- **Fix**: Added shared side-effect-free projection routines for WIBOR, prepayment, and bank installment changes. Preview maps the projection; persistence saves the same projected rows and metadata.
- **Decision**: FIXED

### F2 - Year groups could retain stale values after recalculation

- **Severity**: WARNING
- **Impact**: LOW
- **Dimension**: Safety & Quality
- **Location**: `src/HouseholdBudgetMate.Web/Components/Pages/LoansPage/LoanSchedulePreviewYearPanel.razor`
- **Detail**: The cache key used only affected date, row count, and boundary dates, so changed amounts with the same date range did not rebuild year groups.
- **Fix**: Track the preview row collection identity and affected date. Added a component test that recalculates identical dates with different amounts.
- **Decision**: FIXED

### F3 - UI success criteria relied on source-text assertions

- **Severity**: WARNING
- **Impact**: MEDIUM
- **Dimension**: Success Criteria
- **Location**: `src/HouseholdBudgetMate.Tests/Tests/Ui/LoanScheduleChangeWorkflowUiTests.cs`
- **Detail**: Existing tests proved only that method names appeared in Razor source, not preview-before-write ordering, input retention, reviewed-version confirmation, or stale conflict behavior.
- **Fix**: Added rendered workflow tests with a recording `ILoanService` for all three operations, back-to-edit state retention, version forwarding, and stale confirmation recovery.
- **Decision**: FIXED

## Verification

- `dotnet test HouseholdBudgetMate.slnx -c Release`: 433 passed, 0 failed, 0 skipped.
- `dotnet build HouseholdBudgetMate.slnx -c Release`: passed, including MSI generation.
- Focused `LoanServiceTests`: 52 passed, preserving existing financial fixtures.
- Focused preview/workflow UI tests: 7 passed.
- `git diff --check`: passed.
- Manual responsive, dark-mode, and representative long-mortgage walkthroughs remain pending in the plan.
