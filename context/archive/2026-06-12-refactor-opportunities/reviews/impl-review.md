<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: refactor-opportunities

- **Plan**: `context/changes/refactor-opportunities/plan.md`
- **Scope**: Full implementation after phase 5 request
- **Date**: 2026-06-13
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 1 observation

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | WARNING |
| Pattern Consistency | WARNING |
| Success Criteria | WARNING |

## Automated Checks

- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~AuditTrailTests|FullyQualifiedName~UserScopingTests"`: PASS, 25 tests.
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~MonthlyBudgetingLoopRenderedTests|FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~IncomeServiceTests"`: PASS, 131 tests.
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`: PASS, 401 tests.
- `dotnet build HouseholdBudgetMate.slnx -c Release`: PASS, with existing `MUD0002` warning in `PlanPage.razor`.
- `git diff --check -- .`: PASS, with LF/CRLF warnings only.

## Verified Non-Findings

- Apply suggestions preserves the intended order: `RefreshArchiveMonthsCacheAsync` is passed as the before-refresh callback, then the `BypassPreparation` reload runs. Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:415`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:112`.
- Skip suggestions uses `BypassPreparation` reload, then `RefreshArchiveMonthsCacheAsync` after refresh. Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:460`.

## Findings

### F1 - PIN recorded in acceptance evidence

- **Severity**: WARNING
- **Impact**: LOW - quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: `context/changes/refactor-opportunities/acceptance-evidence.md:20`
- **Detail**: The browser smoke notes record the exact PIN used for login. Even if this is a local/dev value, it is credential-shaped data and should not be committed into repository history.
- **Fix**: Redact the concrete value, for example "entered configured local test PIN", and keep any real local credential only outside tracked repo context.
- **Decision**: FIXED - redacted the concrete PIN value from `acceptance-evidence.md`.

### F2 - Shared actual-amount helper sits in Services namespace and is imported by Mapping

- **Severity**: WARNING
- **Impact**: LOW - quick decision; fix is obvious and narrowly scoped
- **Dimension**: Architecture
- **Location**: `src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs:2`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2965`
- **Detail**: `ExpenseExtensionMapping` imports `HouseholdBudgetMate.Application.Services` because `ExpenseActualAmountCalculator` lives at the bottom of `ExpenseService.cs`. This works, but it makes Mapping depend on the Services namespace for a shared invariant helper and hides reusable actual-amount logic in a service file.
- **Fix**: Move `ExpenseActualAmountCalculator` into a neutral application file/namespace such as `Application/Expenses` or `Application/Calculations`, then update both `ExpenseService` and `ExpenseExtensionMapping` to depend on that neutral helper.
- **Decision**: FIXED - moved `ExpenseActualAmountCalculator` to `HouseholdBudgetMate.Application.Helpers` and updated mapping to depend on that neutral namespace.

### F3 - Acceptance evidence is incomplete and internally inconsistent

- **Severity**: WARNING
- **Impact**: MEDIUM - real tradeoff; pause to reason through it
- **Dimension**: Success Criteria
- **Location**: `context/changes/refactor-opportunities/acceptance-evidence.md:7`, `context/changes/refactor-opportunities/acceptance-evidence.md:27`, `context/changes/refactor-opportunities/acceptance-evidence.md:38`, `context/changes/refactor-opportunities/plan.md:370`
- **Detail**: The evidence lists commands but not explicit pass/fail results. Browser smoke first says income, savings, suggestions, and target-month copy were not fully exercised, then later says income/savings/month copy were exercised, while suggestion apply/skip remained unavailable. The plan requires build/test results and browser notes for expense, income, savings, line-item, suggestion skip/apply, and target-month copy flows.
- **Fix**: Rewrite the evidence into explicit "passed", "partially covered", and "not covered" sections with command results. Either complete suggestion skip/apply smoke using a state that surfaces the panel, or mark that flow as not covered and leave the corresponding manual progress item pending.
- **Decision**: FIXED - rewrote `acceptance-evidence.md` with explicit command results and separated browser smoke into passed and not-covered sections; suggestion apply/skip remains documented as not covered and manual progress 5.6 remains pending.

### F4 - UI guard tests assert helper presence more than handler behavior

- **Severity**: OBSERVATION
- **Impact**: LOW - quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:57`, `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:89`
- **Detail**: The source-level UI tests assert private helper names and broad string absence/presence. They can fail on harmless helper renames while still not proving each handler uses the correct refresh mode and callback order.
- **Fix**: Keep the source guardrails, but make them handler-to-behavior oriented: assert the relevant handler blocks map to the required refresh modes/callbacks, especially apply/skip suggestions and target-month copy.
- **Decision**: FIXED - added handler-block source assertions for target-month copy, apply suggestions, and skip suggestions so the tests pin refresh mode and callback placement instead of only broad helper presence.
