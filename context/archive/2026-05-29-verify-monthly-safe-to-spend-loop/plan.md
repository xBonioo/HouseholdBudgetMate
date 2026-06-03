# Verify Monthly Budgeting Loop Implementation Plan

## Overview

Complete the S-02 roadmap slice: a PIN-unlocked household member can run the monthly budgeting loop end to end, from opening a month and entering planned/real/unexpected expenses to seeing trustworthy month-state values. The accepted scope no longer includes a separate safe-to-spend amount; acceptance focuses on `Live balance`, `Pozostalo w planie`, savings transfer timing, incomplete-balance guidance, and the month lifecycle.

The implementation must not treat this as a casual demo. The user chose the strict readiness path: before real household data is entered, the pending manual items from `secure-real-data-readiness` must be completed or explicitly signed off according to that evidence file. The S-02 acceptance scenario remains controlled demo data.

## Current State Analysis

The roadmap defines S-02 as the north-star user-visible slice: a household member can manage a month and see reliable month finances. Its prerequisites are marked done: F-01 established the financial contract, F-02 established the real-data readiness gate, and S-01 implemented PIN-gated access.

The current code already exposes `Live balance`, plan KPI progress, expense editing, savings transfers, month close/reopen behavior, and balance-base completeness guidance. The separate safe-to-spend contract was considered during planning, rejected by the user on 2026-05-29, and reaffirmed as out of scope on 2026-05-30; the short-lived phase 2 implementation changes were rolled back. S-02 should now validate the existing monthly budgeting loop without adding that value back.

The month workflow itself already exists. `PlanPage` loads the month, categories, accounts, incomes, dashboard summary, and live balance; expense creation/editing and line item changes call `LoadAsync` after saving. The current test project has service and setup tests but no browser/component automation infrastructure. Because the user selected browser/component automation, this plan adds a new UI automation layer specifically for the S-02 loop.

## Desired End State

A secured household profile can unlock the app, open the target month, create planned expenses, record real spending against a planned expense, add an unexpected expense, manage savings transfer timing, close and reopen the month, edit it again, and close it once more. The visible financial result distinguishes:

- `Live balance`: current liquidity based on preceding closing balances, due income, actual expenses, and due savings transfers.
- `Pozostalo w planie`: plan progress based on planned, actual, and unexpected expenses.
- Savings transfer timing: due transfers reduce `Live balance`; future transfers remain visible in the month plan without being deducted yet.
- Incomplete balance-base guidance: missing preceding closing balances show corrective guidance instead of implying a trustworthy live value.

The accepted demonstration uses a controlled scenario with known expected numbers. Automated service/integration tests prove the formulas and lifecycle behavior, and a browser/component test proves the clicked monthly loop. `context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md` records the scenario data, expected values, command results, browser evidence, and final human sign-off.

## Key Discoveries

- [roadmap.md](F:/Kamil/.Net/_projects/HouseholdBudgetMate/context/foundation/roadmap.md:104) defines S-02 as the full monthly loop and depends on F-01, F-02, and S-01.
- [LiveBalanceDto.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Abstractions/Contracts/Incomes/Dto/LiveBalanceDto.cs:3) exposes live-balance components and balance-base completeness.
- [IncomeService.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Application/Services/IncomeService.cs:399) returns live balance, due income, actual expenses, due savings transfers, and completeness.
- [PlanPage.razor](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:101) already shows incomplete-balance guidance, and [PlanPage.razor](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:123) shows `Live balance`.
- [PlanPage.Expenses.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:57) saves new expenses and reloads the page state, which is the expected update path for month KPI and live-balance changes.
- [Home.razor](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Components/Pages/Home.razor:160) and [Accounts.razor](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor:78) already participate in balance completeness messaging.
- [HouseholdBudgetMate.Tests.csproj](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj:1) has xUnit, EF test providers, Moq, and architecture tests, but no browser/component test package yet.
- [readiness-evidence.md](F:/Kamil/.Net/_projects/HouseholdBudgetMate/context/changes/secure-real-data-readiness/readiness-evidence.md:1) still has pending manual readiness items; the user chose to require these before S-02.

## What We're NOT Doing

- Adding a separate safe-to-spend value, reserve fields, or UI KPI.
- Reopening the F-01 formula decisions; S-02 now validates the accepted current financial-result model.
- Redesigning PIN access, profile administration, or the trusted-session model from S-01.
- Replacing the real-data readiness process from F-02; S-02 consumes its evidence gate.
- Migrating financial rows away from the technical shared-budget owner.
- Adding OCR, receipt parsing, recurring generation, next-month copy automation, or public Web API scope.
- Running the acceptance scenario on real household data unless F-02 evidence and a separate human sign-off allow it.

## Implementation Approach

Run S-02 as a gated acceptance slice. First complete the F-02 manual readiness evidence, because the user selected the strict readiness option. Then add deterministic service/integration coverage for the controlled scenario and month lifecycle using the accepted current financial-result model. Finally, add a browser/component automation path that exercises the clicked workflow and records the result in a dedicated S-02 evidence artifact.

Use the existing application services as the source of business truth. UI components may format and branch on completeness, but they must not recalculate live balance or plan KPI values locally.

## Critical Implementation Details

### Accepted Financial Result Model

Do not add safe-to-spend back. The accepted phase 3 assertions should use existing service outputs:

- `Live balance = AccountsBaseTotal + due IncomesTotal - actual ExpensesTotal - due SavingsTransfersTotal`.
- `Pozostalo w planie` comes from existing month KPI behavior.
- Future savings transfers do not reduce `Live balance` until their transfer date is due.
- Missing preceding closing balances keep `HasCompleteBalanceBase` false and preserve missing-account guidance.

### Readiness Gate

Before real household data is entered, `context/changes/secure-real-data-readiness/readiness-evidence.md` must show the pending manual items as completed or explicitly human-approved. This includes backup, restore smoke test, admin readiness panel review, and live readiness checks as applicable. If these cannot be completed, the application can still be verified with controlled demo data, but real-data MVP use must stay blocked in the evidence file.

### Browser Automation Scope

The repo currently has no UI automation framework. Add the smallest browser/component test surface that can prove this slice without turning the change into broad test-platform work. The automation should seed or create a deterministic test state, unlock a visible PIN profile, exercise the month flow, and assert user-visible values. Avoid relying on fragile styling selectors where semantic labels, routes, text, or test-friendly attributes can be used.

## Phase 1: Readiness and Baseline Gate

### Overview

Confirm S-02 is allowed to proceed by completing the strict real-data readiness gate and recording the starting regression/baseline.

### Changes Required:

#### 1. F-02 Evidence Completion

**File**: `context/changes/secure-real-data-readiness/readiness-evidence.md`

**Intent**: Ensure the operational readiness gate selected by the user is closed before S-02 implementation and acceptance starts.

**Contract**: Update the pending manual items with concrete evidence or an explicit human-approved exception: `pg_dump`, restore smoke test, `/health/ready`, Render Blueprint/manual validation, admin readiness panel review, and final readiness sign-off.

#### 2. S-02 Acceptance Evidence Template

**File**: `context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md`

**Intent**: Create a durable record for the controlled monthly-loop scenario and its expected values.

**Contract**: Include sections for readiness prerequisite status, controlled scenario inputs, expected formula breakdown, automated command results, browser/component evidence, close/reopen observations, and final human sign-off.

#### 3. Scope Baseline Note

**File**: `context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md`

**Intent**: Document the accepted starting point and the explicit removal of the separate safe-to-spend scope.

**Contract**: Record references to current `LiveBalanceDto`, `IncomeService`, and UI surfaces so reviewers understand which values S-02 will verify.

### Success Criteria:

#### Automated Verification:

- Evidence files exist: `Test-Path context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md; Test-Path context/changes/secure-real-data-readiness/readiness-evidence.md`
- Baseline build still passes before S-02 implementation changes: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual Verification:

- F-02 readiness evidence is completed or explicitly approved for S-02 by the user.
- User confirms S-02 may use a controlled demo scenario after the strict readiness gate is satisfied.

**Implementation Note**: Do not proceed to Phase 2 until this phase is manually confirmed. If F-02 evidence remains pending, stop and resolve it first.

---

## Phase 2: Re-scope Monthly Financial Result Visibility

### Overview

Confirm the accepted S-02 surface after the user rejected the separate safe-to-spend amount.

### Changes Required:

#### 1. Contract Scope Confirmation

**File**: `src/HouseholdBudgetMate.Abstractions/Contracts/Incomes/Dto/LiveBalanceDto.cs`

**Intent**: Preserve the current live-balance contract without adding safe-to-spend or reserve fields.

**Contract**: `LiveBalanceDto` should continue to expose live-balance components and completeness only.

#### 2. Financial Result Calculation Baseline

**File**: `src/HouseholdBudgetMate.Application/Services/IncomeService.cs`

**Intent**: Keep `GetLiveBalanceAsync` as the source for live liquidity and completeness.

**Contract**: Keep the existing account-base completeness and archive rules. Due savings transfers reduce live balance; future savings transfers do not.

#### 3. Financial Contract Tests

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/IncomeServiceTests.cs`

**Intent**: Protect the accepted live-balance semantics that phase 3 will compose into the loop scenario.

**Contract**: Existing live-balance tests should continue covering due income, actual expenses, due/future savings transfers, account-type exclusion, archived accounts, and incomplete balance bases.

#### 4. Core Screen Visibility Baseline

**Files**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.cs`

**Intent**: Confirm the core workflow screen visibly exposes the accepted values.

**Contract**: `PlanPage` should show `Pozostalo w planie`, `Live balance`, savings summary, and incomplete-balance guidance. It should not show safe-to-spend.

#### 5. Dashboard and Accounts Consistency

**Files**: `src/HouseholdBudgetMate.Web/Components/Pages/Home.razor`, `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor`, `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs`

**Intent**: Keep S-02 from passing on one screen while other primary surfaces drift.

**Contract**: Dashboard and Accounts should show the same live-balance and incomplete-state semantics as Plan where applicable. They should not show safe-to-spend.

### Success Criteria:

#### Automated Verification:

- Financial-result baseline tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~IncomeServiceTests"`
- Web project compiles with the accepted DTO contract: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual Verification:

- Plan, Dashboard, and Accounts show accepted live-balance/month-state values without safe-to-spend.
- The same screens show incomplete-data guidance rather than a trustworthy live value when preceding closing balances are missing.

**Implementation Note**: Pause after this phase so the user can confirm the visible terminology and screen placement before deeper acceptance automation is added.

---

## Phase 3: Prove the Controlled Monthly Scenario in Services

### Overview

Turn the chosen acceptance data into durable automated service/integration coverage.

### Changes Required:

#### 1. Controlled Scenario Test Data

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs`

**Intent**: Create one clear executable scenario matching the manual acceptance path.

**Contract**: Seed or create a PIN-protected household profile, a non-savings account with preceding closing balance, current-month income, a planned expense, a real spend against that expense, an unexpected expense, and a future savings transfer.

#### 2. Financial Result Assertion

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs`

**Intent**: Assert the expected financial result after each important state change.

**Contract**: Verify live balance, plan KPI values, due/future savings transfer behavior, and incomplete balance-base behavior after initial planning, real spending, unexpected expense entry, and savings transfer timing.

#### 3. Month Lifecycle Assertion

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs`

**Intent**: Cover the user-selected `close -> reopen -> edit -> close` workflow.

**Contract**: Assert closed months reject ordinary edits, reopening allows the planned/actual expense change, and closing again returns the month to read-only while preserving final live-balance and plan KPI values.

#### 4. Access Boundary Assertion

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs`

**Intent**: Keep S-02 tied to the PRD condition that budget data is behind PIN unlock.

**Contract**: Reuse S-01 patterns to assert the scenario operates under a visible PIN-protected profile and that an unset interactive scope does not read/write the household budget.

### Success Criteria:

#### Automated Verification:

- Monthly-loop service tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- Related financial and access tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~IncomeServiceTests|FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~UserSession|FullyQualifiedName~UserScoping"`

#### Manual Verification:

- Review the controlled scenario table in `acceptance-evidence.md` and confirm the expected numbers match household budgeting intuition.

**Implementation Note**: The service scenario is the source of truth for expected values used later by the browser/component test and manual evidence.

---

## Phase 4: Add Browser/Component Acceptance Automation

### Overview

Add a minimal UI automation layer and prove the clicked monthly loop selected by the user.

### Changes Required:

#### 1. UI Test Infrastructure

**File**: `src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj`

**Intent**: Add the smallest supported browser/component test dependencies needed to run an end-to-end Blazor Server acceptance flow.

**Contract**: Introduce test packages and any test host helpers required for browser/component automation. Keep the setup scoped to tests; no production runtime dependency should be added.

#### 2. Test Host and Data Seeding Helper

**Files**: new focused helpers under `src/HouseholdBudgetMate.Tests/Tests/Ui/` or `src/HouseholdBudgetMate.Tests/Shared/`

**Intent**: Start the web app in a deterministic test mode with a seeded visible PIN profile and controlled S-02 data.

**Contract**: The helper must avoid real production data and must not depend on Render. It should seed or reset only the test database/store used by the UI test.

#### 3. Monthly Loop Browser/Component Test

**File**: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`

**Intent**: Exercise the user-visible workflow rather than only service calls.

**Contract**: Automate: unlock profile with PIN, open selected month, create planned expense, record actual expense, add unexpected expense, confirm visible live-balance and plan KPI changes, close month, verify edit controls are blocked, reopen month, edit a value, close again, and confirm final visible month state.

#### 4. Stable UI Selectors

**Files**: relevant Razor components touched by the UI test

**Intent**: Keep browser/component tests maintainable without coupling to visual layout classes.

**Contract**: Add accessible labels or minimal test-friendly attributes only where existing labels/text are insufficient. Do not add visible instructional text solely for tests.

### Success Criteria:

#### Automated Verification:

- UI acceptance test passes with its required test command documented by implementation: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- Full release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`

#### Manual Verification:

- Browser/component test evidence is recorded in `acceptance-evidence.md`, including command output and any screenshot/log path if the chosen harness produces one.
- The user confirms the automated clicked path matches the intended household workflow.

**Implementation Note**: If browser automation proves impractical in the existing Blazor Server setup, stop and review before falling back. The user explicitly selected browser/component automation, so replacing it with manual-only verification requires user approval.

---

## Phase 5: Final Acceptance Evidence and Roadmap Closure

### Overview

Record the final proof that S-02 satisfies the PRD slice and update change tracking.

### Changes Required:

#### 1. Acceptance Evidence Completion

**File**: `context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md`

**Intent**: Make the final result auditable and readable after the implementation context is gone.

**Contract**: Fill in readiness prerequisite status, final controlled scenario values, automated command results, UI automation evidence, close/reopen observations, and human sign-off.

#### 2. Change Progress and Status

**Files**: `context/changes/verify-monthly-safe-to-spend-loop/change.md`, `context/changes/verify-monthly-safe-to-spend-loop/plan.md`

**Intent**: Mark completed verification items and identify the implemented commit(s).

**Contract**: Update `change.md` status from `planned` to `implemented` only after all automated and manual S-02 success criteria are satisfied. Mark Progress checkboxes with commit hashes following repository convention.

#### 3. Roadmap Status

**File**: `context/foundation/roadmap.md`

**Intent**: Reflect that the north-star S-02 slice is complete.

**Contract**: Change S-02 status from `proposed` to `done` only when acceptance evidence has final human sign-off. Do not mark done while F-02 readiness or browser acceptance remains pending.

### Success Criteria:

#### Automated Verification:

- Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- Full release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`
- Git whitespace check passes: `git diff --check -- .`

#### Manual Verification:

- `acceptance-evidence.md` contains final expected-vs-actual values for the controlled scenario.
- User confirms the clicked loop proves S-02: PIN unlock, month plan, planned expense, real expense, unexpected expense, live-balance/plan KPI updates, close/reopen/edit/close.
- User confirms roadmap S-02 may be marked done.

**Implementation Note**: This is the release gate for the roadmap north star. Do not close S-02 if any readiness, formula, UI automation, or human sign-off item remains pending.

---

## Testing Strategy

### Unit Tests:

- Keep `IncomeServiceTests` protecting accepted live-balance totals, due/future savings transfers, and balance-base completeness.
- Keep `ExpenseServiceTests` covering `MonthPlanKpiDto.RemainingTotal` as plan progress only.
- Reuse S-01 session/scoping tests rather than duplicating all access-hardening coverage.

### Integration Tests:

- Add `MonthlyBudgetingLoopTests` for the deterministic service-level scenario across account balances, income, planned expense, actual expense, unexpected expense, savings transfer, and month lifecycle.
- Include the strict readiness evidence check as a manual prerequisite, not as a fragile automated parser of a human evidence file.

### Browser/Component Tests:

- Add a narrow UI acceptance test harness for the monthly loop.
- Prefer semantic selectors, visible labels, accessible names, or tiny test attributes over CSS layout selectors.
- Assert visible text/amounts for `Live balance`, `Pozostalo w planie`, savings transfer timing, incomplete-state guidance, closed-month edit blocking, and reopened-month edit success.

### Manual Testing Steps:

1. Confirm `secure-real-data-readiness/readiness-evidence.md` is signed off according to Phase 1.
2. Use a controlled scenario month with a preceding non-savings closing balance, due income, planned expense, real spending, unexpected spending, and future savings transfer.
3. Verify `Live balance`, `Pozostalo w planie`, savings transfer timing, and incomplete-state guidance after each change.
4. Close the month and confirm ordinary expense edits are blocked.
5. Reopen the month, change a planned/actual value, confirm visible month values update, and close again.
6. Confirm Dashboard and Accounts show the same live-balance and incomplete-state semantics.
7. Record final actual values and user sign-off in `acceptance-evidence.md`.

## Performance Considerations

The formula remains a small-household monthly aggregation over accounts, incomes, expenses, and savings transfers. Phase 3 should reuse existing service calculations without adding per-row UI calculations or new stored derived values. Browser/component tests may add runtime to the test suite; keep the UI acceptance scope narrow and allow filtering by `MonthlyBudgetingLoopUiTests` for targeted runs.

## Migration Notes

No schema migration is planned. The change adds tests and acceptance evidence around existing service/UI behavior. If the browser test harness needs a test-only configuration path or seed helper, keep it outside production data and avoid changing persisted domain schema unless implementation uncovers an unavoidable testability issue that needs review.

## References

- Roadmap item: `context/foundation/roadmap.md` (`S-02`)
- Product requirements: `context/foundation/prd.md` (`US-01`, `FR-001`, `FR-002`, `FR-005`, `FR-006`, `FR-007`)
- Financial baseline: `src/HouseholdBudgetMate.Application/Services/IncomeService.cs`
- Access prerequisite: `context/changes/verify-pin-gated-household-access/plan.md`
- Readiness prerequisite: `context/changes/secure-real-data-readiness/readiness-evidence.md`
- Current DTO: `src/HouseholdBudgetMate.Abstractions/Contracts/Incomes/Dto/LiveBalanceDto.cs`
- Current aggregation: `src/HouseholdBudgetMate.Application/Services/IncomeService.cs`
- Core UI workflow: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor`
- Test project: `src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` - <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Readiness and Baseline Gate

#### Automated

- [x] 1.1 Evidence files exist: `Test-Path context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md; Test-Path context/changes/secure-real-data-readiness/readiness-evidence.md`
- [x] 1.2 Baseline build still passes before S-02 implementation changes: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual

- [x] 1.3 F-02 readiness evidence is completed or explicitly approved for S-02 by the user
- [x] 1.4 User confirms S-02 may use a controlled demo scenario after the strict readiness gate is satisfied

### Phase 2: Re-scope Monthly Financial Result Visibility

> 2026-05-29 scope update: user explicitly rejected the separate safe-to-spend value. The phase 2 implementation changes were rolled back; proceed with the accepted live-balance/month-state model.

#### Automated

- [x] 2.1 Financial-result baseline tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~IncomeServiceTests"`
- [x] 2.2 Web project compiles with the accepted DTO contract: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual

- [x] 2.3 Plan, Dashboard, and Accounts show accepted live-balance/month-state values without a separate safe-to-spend value
- [x] 2.4 The same screens show incomplete-data guidance rather than a trustworthy live value when preceding closing balances are missing

### Phase 3: Prove the Controlled Monthly Scenario in Services

#### Automated

- [x] 3.1 Monthly-loop service tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- [x] 3.2 Related financial and access tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~IncomeServiceTests|FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~UserSession|FullyQualifiedName~UserScoping"`

#### Manual

- [x] 3.3 Review the controlled scenario table in `acceptance-evidence.md` and confirm the expected numbers match household budgeting intuition

### Phase 4: Add Browser/Component Acceptance Automation

#### Automated

- [x] 4.1 UI acceptance test passes with its required test command documented by implementation: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- [x] 4.2 Full release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`

#### Manual

- [x] 4.3 Browser/component test evidence is recorded in `acceptance-evidence.md`
- [x] 4.4 User confirms the automated clicked path matches the intended household workflow

### Phase 5: Final Acceptance Evidence and Roadmap Closure

#### Automated

- [x] 5.1 Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- [x] 5.2 Full release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`
- [x] 5.3 Git whitespace check passes: `git diff --check -- .`

#### Manual

- [x] 5.4 `acceptance-evidence.md` contains final expected-vs-actual values for the controlled scenario
- [x] 5.5 User confirms the clicked loop proves S-02
- [x] 5.6 User confirms roadmap S-02 may be marked done
