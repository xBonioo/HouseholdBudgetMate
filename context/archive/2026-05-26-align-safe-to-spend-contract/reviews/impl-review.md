<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Align Safe-to-Spend Contract

- **Plan**: `context/changes/align-safe-to-spend-contract/plan.md`
- **Scope**: All completed phases (1-3)
- **Date**: 2026-05-26
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 1 observation

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | WARNING |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 - Safe-to-spend is hidden instead of presented as a primary result

- **Severity**: WARNING
- **Impact**: LOW - quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:121`, `src/HouseholdBudgetMate.Web/Components/Pages/Home.razor:168`, `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor:86`
- **Detail**: The current UI provides `Safe to spend` only through tooltip content on the visible `Live balance` result, rather than showing both indicators side by side as planned.
- **Fix**: Restore a visible `Safe-to-spend` indicator and reserve the tooltip for explanatory details.
- **Decision**: ACCEPTED - keep tooltip-only presentation.

### F2 - Balance-base rules disagree between documentation and executable behavior

- **Severity**: WARNING
- **Impact**: HIGH - architectural stakes; think carefully before deciding
- **Dimension**: Architecture
- **Location**: `src/HouseholdBudgetMate.Application/Services/IncomeService.cs:419`, `docs/DOMAIN.md:121`, `src/HouseholdBudgetMate.Tests/Tests/Services/IncomeServiceTests.cs:1316`
- **Detail**: Runtime requires a balance for the immediately preceding month while documentation previously described the most recent earlier closing balance.
- **Fix A**: Select the latest recorded prior balance and update the incomplete test.
- **Fix B**: Keep exact preceding-month enforcement and update the contract and test wording.
- **Decision**: FIXED via Fix B - `docs/DOMAIN.md` and `plan.md` now state immediately preceding-month semantics, and the older-balance test wording matches that contract.

### F3 - Archived accounts can block completeness without an ordinary repair path

- **Severity**: WARNING
- **Impact**: HIGH - architectural stakes; think carefully before deciding
- **Dimension**: Safety & Quality
- **Location**: `src/HouseholdBudgetMate.Application/Services/IncomeService.cs:409`, `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs:633`
- **Detail**: Live-balance completeness included archived accounts for open months although the editable account balance flow excludes them.
- **Fix**: Define archive applicability for the selected month and apply it consistently in aggregation and editing.
- **Decision**: FIXED - archived accounts participate only in selected months completed before their archive timestamp; an account archived during a month is not required to provide that month's closing balance. `UpdatedAtUtc` is a fallback for archived legacy records without `ArchivedAtUtc`. A stored `0,00` balance is accepted as complete data, while the Accounts UI marks an unsaved zero default as `Brak zapisu`. Closed historical months now calculate from prior persisted data without blocking on missing retroactive rows. Added service tests for accounts archived before, during and after the selected month, for a stored zero balance, and for a closed month with incomplete historical entries.

### F4 - Unrelated behavior changes are bundled into this feature

- **Severity**: OBSERVATION
- **Impact**: LOW - quick decision; fix is obvious and narrowly scoped
- **Dimension**: Scope Discipline
- **Location**: `HouseholdBudgetMate.slnx:2`, `src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor:18`, `src/HouseholdBudgetMate.Web/Components/Pages/Audit.razor:15`, `src/HouseholdBudgetMate.Web/Components/Pages/Home.razor:37`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:398`
- **Detail**: The implementation includes solution/page-title cleanup, dashboard action changes, and income edit submission behavior outside the safe-to-spend contract.
- **Fix**: Separate or document these companion changes before merge.
- **Decision**: PENDING

## Verification

- PASS: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~IncomeServiceTests|FullyQualifiedName~AccountServiceTests|FullyQualifiedName~ExpenseServiceTests"` (`125` passed)
- PASS: `dotnet build HouseholdBudgetMate.slnx -c Release` (0 warnings, 0 errors)
- PASS: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build` (`259` passed)
- NOTE: `Debug` verification could not rebuild while the running `HouseholdBudgetMate.Web` process held its output assemblies; `Release` uses separate outputs.
- PASS: `git diff --check -- .`
