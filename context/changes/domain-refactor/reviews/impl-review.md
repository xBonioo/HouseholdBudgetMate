<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Domain Refactor Implementation Plan

- **Plan**: `context/changes/domain-refactor/plan.md`
- **Scope**: Phases 1-3 of 3
- **Date**: 2026-06-14
- **Verdict**: PASS
- **Findings**: 0 critical, 0 warnings, 1 observation

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Summary

The implementation matches the plan's core intent:

- `MonthlyFinancialPictureDto` was added as a public monthly reconciliation contract.
- `IExpenseService` and `ExpenseService` expose and compose the monthly picture.
- Plan and Accounts now consume the monthly picture where they previously stitched month plan and live balance separately.
- Effective actual amount semantics were centralized for the live-balance path.
- Negative line-item amounts are rejected at validation.
- Backup restore recalculates parent actual amounts after restoring line items.
- Recurring generated-row semantics are documented and covered by a direct test.
- Closed-month savings-transfer affordances are visibly disabled in the Plan UI.

No EF migrations or snapshots were changed. The `Safe-to-spend` wording was not reintroduced.

## Verification

Fresh review verification run on 2026-06-14:

- PASS: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~MonthlyBudgetingLoopRenderedTests"`: 11 passed.
- PASS: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~IncomeServiceTests|FullyQualifiedName~BackupServiceTests|FullyQualifiedName~AccountServiceTests"`: 174 passed.
- PASS: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build`: 407 passed.
- PASS: `dotnet build HouseholdBudgetMate.slnx -c Release`: build succeeded with the existing MudBlazor `MUD0002` analyzer warning in `PlanPage.razor`.
- PASS: `git diff --check -- .`: no whitespace errors; only line-ending warnings.

## Findings

### F1 - Manual verification gates are still pending

- **Severity**: WARNING
- **Impact**: LOW - quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: `context/changes/domain-refactor/plan.md:414`
- **Detail**: All automated progress rows for phases 1-3 are checked, but every manual progress row remains unchecked. This is consistent with the implementation gate pausing for human verification, but it means the plan is not ready to be treated as fully accepted or archived yet.
- **Fix**: Complete the manual checks from phases 1-3, then mark those rows only after confirmation.
- **Decision**: CONFIRMED - user confirmed manual verification on 2026-06-14; plan rows and acceptance evidence were updated.

### F2 - Pre-existing dirty file outside this change scope

- **Severity**: OBSERVATION
- **Impact**: LOW - quick decision; fix is obvious and narrowly scoped
- **Dimension**: Scope Discipline
- **Location**: `src/HouseholdBudgetMate.Tests/Tests/Services/LoanServiceTests.cs:497`
- **Detail**: `LoanServiceTests.cs` has an unrelated date change and is outside the `domain-refactor` plan. This file was already dirty before the domain-refactor implementation work, so it is not treated as implementation drift. It should remain outside any domain-refactor commit unless the user explicitly includes it.
- **Fix**: Stage only the domain-refactor touched-file set during the implementation commit ritual.
- **Decision**: PENDING
