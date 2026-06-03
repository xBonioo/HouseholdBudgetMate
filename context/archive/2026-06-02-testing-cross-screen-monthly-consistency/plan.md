# Testing Cross Screen Monthly Consistency Implementation Plan

## Overview

Extend the Phase 1 test rollout so monthly edits are protected across the service projections that feed Plan, Dashboard/Home, Accounts, and Statistics. The change also adds a small rendered Blazor smoke layer and preserves the accepted no-Safe-to-spend UI contract.

## Current State Analysis

The accepted S-02 monthly loop already has strong service coverage and static UI contract checks, but it does not yet prove cross-screen agreement after edits. `MonthlyBudgetingLoopTests` reads `ExpenseService.GetMonthAsync` and `IncomeService.GetLiveBalanceAsync`; Dashboard/Home and Statistics have separate projection paths that are tested elsewhere but not in the same edited monthly scenario.

The current UI contract tests are source-text checks in `MonthlyBudgetingLoopUiTests`, not rendered component tests. Research and bUnit docs show a rendered smoke layer is feasible with xUnit, but this should stay narrow because full PlanPage rendering would pull in MudBlazor, dialogs, routing, and many service dependencies.

## Desired End State

One deterministic edited monthly scenario proves that the backing projections for all Phase 1 screen roles agree after reload: month plan, live balance, dashboard summary, and year statistics. A small bUnit rendered smoke test verifies the accepted monthly UI contract can render with service-provided state, while static UI contract checks continue to guard screen-specific labels, incomplete-balance guidance, and the absence of `Safe-to-spend` / `SafeToSpend`.

### Key Discoveries:

- `MonthlyBudgetingLoopTests` already seeds the accepted household/month scenario and verifies final `7075.00` live balance and `800.00` plan remaining (`src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:23`).
- Plan reloads month, dashboard summary, incomes, and live balance together (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:24`).
- Home reads current-month live balance and dashboard state on load (`src/HouseholdBudgetMate.Web/Components/Pages/Home.razor:359`).
- Accounts reads selected-month live balance on load/reload (`src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs:124`).
- Statistics reads annual/monthly finance through `GetYearStatisticsAsync` (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:543`).
- Existing UI tests are static source contracts (`src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:8`).
- bUnit docs for xUnit show the package/test-runner pattern and rendered component APIs; current docs checked through Context7 on 2026-06-02 (`/websites/bunit_dev`).

## What We're NOT Doing

- Not adding browser/e2e or Playwright coverage in this phase.
- Not forcing Statistics to display `Live balance`; it remains annual/monthly finance context.
- Not adding a full rendered test for Plan, Home, Accounts, and Statistics.
- Not changing production financial formulas, DTO contracts, schema, or UI copy unless a test exposes an actual regression.
- Not calculating expected values in tests from the production formulas.
- Not reopening the superseded Safe-to-spend product decision.

## Implementation Approach

Use the existing monthly-loop service fixture as the primary protection layer. Extend it so the accepted scenario reads the same projections each screen depends on after key mutations and asserts expected values from `acceptance-evidence.md`, not from duplicated service formulas.

Add bUnit in the existing test project as a narrow rendered smoke layer. Prefer C# test classes over `.razor` test files to avoid unnecessary test-project SDK churn; if rendering requires an SDK or package adjustment, keep it scoped to `HouseholdBudgetMate.Tests.csproj` and verify with build plus targeted tests.

Then tighten static UI contract checks and update the rollout cookbook so future agents know when to choose service projection integration, static UI contract, rendered smoke, or browser testing.

## Critical Implementation Details

### Oracle Source

Use `context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md` as the independent oracle for expected numbers. Do not compute expected values with the same formulas as `ExpenseService` or `IncomeService`.

### Rendered Smoke Scope

The rendered smoke test should prove a small, service-provided monthly UI contract renders; it should not click through the full workflow. If rendering `PlanPage` directly requires broad service/dialog/router setup, create a narrow test-only host component under the test project that renders the same accepted labels/state from DTO-like inputs, then keep static source checks as the guard that production pages are wired to those contracts.

## Phase 1: Cross-Screen Projection Integration

### Overview

Extend the accepted service scenario so one edited monthly loop asserts every relevant screen backing projection after reload.

### Changes Required:

#### 1. Monthly Loop Projection Assertions

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs`

**Intent**: Extend the existing controlled monthly-loop scenario so it verifies the service projections used by Plan, Dashboard/Home, Accounts, and Statistics after key edits.

**Contract**: Keep the existing InMemory scoped fixture. Add assertions around `ExpenseService.GetMonthAsync`, `IncomeService.GetLiveBalanceAsync`, `ExpenseService.GetDashboardSummaryAsync`, and `ExpenseService.GetYearStatisticsAsync` for the accepted scenario values.

#### 2. Cross-Projection Helper

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs`

**Intent**: Keep the scenario readable while avoiding repeated projection reads and duplicated expectations.

**Contract**: Extend the current `LoopServices` / `LoopState` helper shape to include dashboard summary and year statistics, with expected values passed as literals from the acceptance evidence.

#### 3. Statistics-Specific Assertion

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs`

**Intent**: Prove Statistics sees the edited month through annual/monthly finance, not through a forced live-balance label.

**Contract**: Assert the relevant `YearStatisticsDto.MonthlyFinance` row for `2026-04` reflects the accepted planned/spent/savings values after edits.

### Success Criteria:

#### Automated Verification:

- Targeted monthly-loop tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- Related projection tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~IncomeServiceTests"`

#### Manual Verification:

- Review the projection assertions and confirm their expected values trace back to `acceptance-evidence.md`, not copied production formula logic.

**Implementation Note**: After completing this phase and all automated verification passes, pause for manual confirmation before proceeding.

---

## Phase 2: Rendered UI Smoke Contract

### Overview

Add a minimal bUnit rendered smoke layer for the accepted monthly UI contract without building a full browser or full-page component test suite.

### Changes Required:

#### 1. bUnit Test Dependency

**File**: `src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj`

**Intent**: Add the smallest dependency surface needed to render Blazor components in xUnit tests.

**Contract**: Add `bunit` to the existing test project. Prefer leaving the project as `Microsoft.NET.Sdk` with C# test classes; only switch to `Microsoft.NET.Sdk.Razor` if the chosen rendered test shape requires it and the full test suite still builds.

#### 2. Rendered Monthly Contract Smoke Test

**File**: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopRenderedTests.cs`

**Intent**: Verify a rendered Blazor contract can display the accepted monthly state from service-provided values.

**Contract**: Use bUnit with xUnit. Render the smallest feasible component/test host that shows accepted monthly labels/state: `Pozostało w planie`, `Live balance`, incomplete-balance guidance when incomplete, and no `Safe-to-spend` / `SafeToSpend`.

#### 3. MudBlazor/Test Services Setup

**File**: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopRenderedTests.cs` or a focused helper under `src/HouseholdBudgetMate.Tests/Shared/`

**Intent**: Keep rendered tests stable without leaking broad UI test infrastructure into unrelated tests.

**Contract**: Register only the services required by the rendered smoke target. If MudBlazor services are needed, register them in the test context setup for this test only.

### Success Criteria:

#### Automated Verification:

- Rendered UI smoke test passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopRenderedTests"`
- Existing static UI contract tests still pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- Solution builds after dependency/test setup changes: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual Verification:

- Review the rendered smoke scope and confirm it remains a contract smoke test, not an accidental full UI automation project.

**Implementation Note**: If bUnit setup expands into full Plan/Home/Accounts/Statistics rendering, stop and review scope before continuing.

---

## Phase 3: Static UI Contract And Cookbook

### Overview

Tighten the existing source-text UI contract and record the shipped testing pattern in the rollout cookbook.

### Changes Required:

#### 1. Static Screen-Role Contract

**File**: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`

**Intent**: Make the static UI guard match each screen's actual role instead of asserting identical wording everywhere.

**Contract**: Assert Plan/Home carry `Pozostało w planie` and `Live balance`; Accounts carries `Live balance` plus account/savings/envelope context and does not present monthly `Pozostało w planie`; Statistics carries monthly/annual finance rollup text and does not require `Live balance`.

#### 2. Safe-to-Spend And Incomplete Guidance Guard

**File**: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`

**Intent**: Keep the superseded Safe-to-spend contract from returning and keep incomplete balance explicit.

**Contract**: Preserve absence checks for `Safe-to-spend` and `SafeToSpend`. Strengthen guidance checks across Plan/Home/Accounts for previous-month closing-balance wording.

#### 3. Rollout Cookbook Entry

**File**: `context/foundation/test-plan.md`

**Intent**: Document the pattern shipped by Phase 1 so future agents know the cheapest useful layer for cross-screen monthly consistency.

**Contract**: Update §6.1 and §6.5 with the reference tests, commands, and decision rule: service projection integration for numeric agreement, static UI contract for screen roles, rendered smoke for minimal rendered confidence, browser/e2e only if already-open-screen behavior becomes the risk.

### Success Criteria:

#### Automated Verification:

- Static and rendered UI contracts pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~MonthlyBudgetingLoopRenderedTests"`
- Full release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`
- Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- Git whitespace check passes: `git diff --check -- .`

#### Manual Verification:

- Review `context/foundation/test-plan.md §6.1` and confirm it explains how to add future cross-screen monthly consistency tests.
- Confirm the final test set protects risks #1 and #4 without adding browser/e2e scope.

**Implementation Note**: After this phase, return to `/10x-test-plan` only if the rollout status/cookbook needs reconciliation; otherwise continue through the normal implementation completion flow.

---

## Testing Strategy

### Unit Tests:

- No new pure unit tests are expected.
- Existing `IncomeServiceTests` and `ExpenseServiceTests` remain the lower-level formula and projection rule coverage.

### Integration Tests:

- Extend `MonthlyBudgetingLoopTests` as the primary Phase 1 protection.
- Assert `GetMonthAsync`, `GetLiveBalanceAsync`, `GetDashboardSummaryAsync`, and `GetYearStatisticsAsync` after accepted scenario mutations.
- Use fixed expected values from `acceptance-evidence.md`.

### Rendered UI Tests:

- Add a minimal bUnit smoke contract for accepted monthly labels/state.
- Keep the rendered scope intentionally narrow.

### Static UI Contract Tests:

- Tighten `MonthlyBudgetingLoopUiTests` for screen-specific roles and stale wording guards.
- Continue using source-text checks where they provide cheap signal for labels and service wiring.

### Manual Testing Steps:

1. Review the added service integration assertions against the controlled scenario table.
2. Review the rendered smoke test target and confirm it did not expand into a broad component test platform.
3. Review the cookbook entry for clear future-agent guidance.

## Performance Considerations

The service integration test adds a few projection reads against an InMemory database. Runtime impact should stay low. If the rendered smoke setup becomes slow or brittle, keep it to one narrow test and avoid adding full-page rendered coverage.

## Migration Notes

No production migration or data migration is planned. The only dependency migration is a test-project package addition for bUnit; verify restore/build/test after the package change.

## References

- Related research: `context/changes/testing-cross-screen-monthly-consistency/research.md`
- Rollout plan: `context/foundation/test-plan.md`
- Accepted oracle: `context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md`
- Existing service scenario: `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:23`
- Existing static UI contract: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:8`
- bUnit docs checked via Context7 on 2026-06-02: `/websites/bunit_dev`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Cross-Screen Projection Integration

#### Automated

- [x] 1.1 Targeted monthly-loop tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- [x] 1.2 Related projection tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~IncomeServiceTests"`

#### Manual

- [x] 1.3 Review the projection assertions and confirm their expected values trace back to `acceptance-evidence.md`, not copied production formula logic

### Phase 2: Rendered UI Smoke Contract

#### Automated

- [x] 2.1 Rendered UI smoke test passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopRenderedTests"` — 7579361
- [x] 2.2 Existing static UI contract tests still pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"` — 7579361
- [x] 2.3 Solution builds after dependency/test setup changes: `dotnet build HouseholdBudgetMate.slnx -c Release` — 7579361

#### Manual

- [x] 2.4 Review the rendered smoke scope and confirm it remains a contract smoke test, not an accidental full UI automation project — 7579361

### Phase 3: Static UI Contract And Cookbook

#### Automated

- [x] 3.1 Static and rendered UI contracts pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~MonthlyBudgetingLoopRenderedTests"` — f494a78
- [x] 3.2 Full release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release` — f494a78
- [x] 3.3 Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release` — f494a78
- [x] 3.4 Git whitespace check passes: `git diff --check -- .` — f494a78

#### Manual

- [x] 3.5 Review `context/foundation/test-plan.md §6.1` and confirm it explains how to add future cross-screen monthly consistency tests — f494a78
- [x] 3.6 Confirm the final test set protects risks #1 and #4 without adding browser/e2e scope — f494a78
