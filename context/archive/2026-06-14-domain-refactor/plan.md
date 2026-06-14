# Domain Refactor Implementation Plan

## Overview

This plan turns the domain distillation and research into a staged refactor around the accepted monthly budgeting contract. The first target is a named monthly reconciliation boundary: `MonthlyFinancialPicture`, covering `MonthPlan`, `Pozostalo w planie`, `Live balance`, complete balance-base guidance, due savings transfers, and closed-month read-only state.

The refactor is deliberately staged. We will introduce a public DTO/read contract in `Abstractions`, compose it inside the application layer, and keep persistence entities unchanged. Only after that boundary is protected will we tighten the adjacent `Expense.ActualAmount` / line-item behavior, backup restore consistency, and closed-month UI affordances.

## Current State Analysis

The monthly picture is currently split across several services. `ExpenseService.GetMonthAsync` loads or creates `MonthPlan`, syncs regular expenses/incomes/loan installments on first open-month creation, loads expenses and savings transfers, and builds `MonthPlanDto`. `ExpenseService` also calculates plan KPI and owns month close/open lifecycle. `IncomeService.GetLiveBalanceAsync` separately computes previous non-savings account base, due incomes, actual expenses, due savings transfers, and balance-base completeness. `AccountService.UpsertMonthBalanceAsync` owns persisted account-month balances and open-month checks.

The domain entities are persistence-oriented by current architecture. `MonthPlan`, `Expense`, `AccountMonthBalance`, regular definitions, and savings transfers have public setters and rely on application services plus EF constraints for behavior. Research confirmed that some suspected DB gaps are already covered: account-month balances have a unique `{ AccountId, Year, Month }` index, generated regular expenses have a unique `{ MonthPlanId, RegularExpenseDefinitionId }` index, and generated regular incomes have a unique `{ Year, Month, RegularIncomeDefinitionId }` index.

The riskiest adjacent inconsistency is effective expense actual amount. `ExpenseDto` mapping uses line-item sum when line items exist, but live balance and some projections trust persisted `Expense.ActualAmount`. Runtime line-item mutations recalculate the parent, but backup restore inserts expenses and line items independently, and line-item amount validation does not currently reject negative amounts.

## Desired End State

By the end of the plan:

- A public `MonthlyFinancialPictureDto` exists in `Abstractions` and exposes the accepted monthly picture as one named read contract.
- Application-layer composition owns the monthly reconciliation logic; UI no longer needs to stitch together `MonthPlanDto`, `LiveBalanceDto`, savings context, and completeness as separate concepts when it needs the full picture.
- Existing `MonthPlanDto` and `LiveBalanceDto` remain available during the transition, so the refactor does not force a broad UI rewrite in one step.
- Effective actual amount semantics are centralized and consistently used by monthly projections, while preserving current final-line-item deletion behavior.
- Negative line-item amounts are rejected unless a future refund/correction feature deliberately introduces them.
- Backup restore recalculates parent actual amounts after restoring line items so restored data cannot drift from runtime projections.
- Closed-month UI controls visually match service-level read-only behavior for savings transfers and the nearby monthly write actions touched by this change.
- No EF schema migration is required.

## Key Decisions

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Monthly boundary implementation | Application-layer builder/policy | Matches current architecture where application services own workflow and domain entities are persistence-oriented. | Research + planning |
| Public contract | Add `MonthlyFinancialPictureDto` in `Abstractions` | Makes the domain language visible at the service boundary without immediately rewriting EF entities. | Planning |
| Scope | Monthly core plus explicit adjacent decisions | Captures the real financial risks around effective actual, restore, validation, and closed-month UI without expanding into envelopes/audit/household access cleanup. | Research + planning |
| Effective actual semantics | Keep persisted parent actual as cache/manual value, centralize effective actual policy | Preserves behavior while reducing drift between Plan, live balance, and statistics. | Research + planning |
| Last line-item deletion | Preserve current behavior and document as contract | Existing tests and behavior expect the last calculated parent actual to remain. | Research + planning |
| Negative line-item amounts | Reject as invalid for now | Refund/correction semantics are not designed; allowing negative spending silently is too risky. | Planning |
| Backup restore | Recalculate after line-item restore | Prevents restored data from drifting between DTO mapping and live balance. | Research + planning |
| Closed-month UI | Minimal UI alignment | Service guards already exist; UI should not invite actions that will fail. | Research + planning |
| Soft-deleted recurring generated rows | Preserve and document current blocking behavior | Current `IgnoreQueryFilters()` checks imply user-deleted generated items should not reappear automatically. | Research + planning |
| Existing data drift | Diagnostic/report first, no automatic migration | Avoids silently rewriting financial history while still surfacing any drift. | Planning |
| Test oracle | Service integration oracle plus focused policy tests and static/rendered UI checks | Protects financial behavior without leaning on brittle broad UI tests. | Planning |
| Phasing | Three phases | Gives clean review checkpoints: monthly boundary, effective actual hardening, UI/evidence cleanup. | Planning |

## What We're NOT Doing

- Not reviving a separate `Safe-to-spend` concept, DTO, KPI, reserve field, or UI label.
- Not converting EF entities into rich aggregates in this change.
- Not changing account-month balance schema or recurring generated-row unique indexes.
- Not changing final-line-item deletion semantics.
- Not introducing refund/correction support for negative line items.
- Not automatically recalculating or migrating all existing production data.
- Not redesigning envelopes, audit trail, household access, loans, or category history.
- Not replacing all existing `GetMonthAsync` / `GetLiveBalanceAsync` call sites at once unless a phase explicitly lists the call site.

## Implementation Approach

Use a strangler-style refactor. First add the named public monthly picture DTO and an application-layer composer that delegates to existing calculation paths or extracted pure policies. Then move calculation logic behind named policies without changing observed values. Once the monthly read model is stable, harden effective actual amount and restore paths. Finish with UI affordance alignment and evidence.

The safest shape is:

1. `Abstractions` defines the public read contract.
2. `Application` composes the DTO and owns policies.
3. Existing services remain the persistence/workflow owners until the new boundary proves itself.
4. UI adoption is incremental and protected by the existing S-02 numeric oracle.

## Critical Implementation Details

### Preserve Current Product Language

The monthly picture is `Live balance`, `Pozostalo w planie`, savings context, and incomplete-balance guidance. Do not add or rename this to `Safe-to-spend`.

### Keep Application as the First Boundary

The builder/policy may use value snapshots internally, but it should not move EF access into domain entities. Avoid pushing DbContext or service dependencies into Domain.

### Effective Actual Must Be One Policy

Every path touched by this plan must either use the same effective actual policy or explicitly document why persisted parent actual is authoritative for that projection.

### No Silent Financial Data Rewrite

Runtime and restore paths may recalculate parent actual amounts when they mutate line items. Existing stored data should only be diagnosed/reported in this plan, not automatically rewritten.

## Phase 1: Monthly Financial Picture Boundary

### Overview

Introduce the public monthly picture contract and application-layer composition while preserving existing service behavior. This phase is mostly additive: it names the domain boundary and proves it returns the same accepted values as the current separate projections.

### Changes Required:

#### 1. Public Monthly Picture DTO

**Files**:

- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto/MonthlyFinancialPictureDto.cs`
- Existing DTO folders if smaller child DTOs are needed

**Intent**: Add a public read contract for the accepted monthly financial picture.

**Contract**: The DTO must expose year/month, `MonthPlanDto`, `LiveBalanceDto`, plan KPI or equivalent plan remaining values, savings transfer context, closed-month state, and balance-base completeness. It must compose existing DTOs where practical rather than duplicating all fields.

#### 2. Service Contract Entry Point

**File**: `src/HouseholdBudgetMate.Abstractions/Interfaces/IExpenseService.cs`

**Intent**: Add a named read method for the monthly picture without forcing the UI to manually combine plan and live balance when it wants the full monthly reconciliation.

**Contract**: Add a method such as `Task<MonthlyFinancialPictureDto> GetMonthlyFinancialPictureAsync(int year, int month, CancellationToken cancellationToken);`. Keep existing `GetMonthAsync` and `IIncomeService.GetLiveBalanceAsync` contracts intact.

#### 3. Application Composer

**File**: `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs`

**Intent**: Implement the new method by composing existing monthly plan and live-balance calculations first, then extracting internal policy pieces only where doing so reduces duplication.

**Contract**: The returned values must match existing `GetMonthAsync` + `GetLiveBalanceAsync` for the same year/month. Month creation and regular sync side effects must remain consistent with current `GetMonthAsync` behavior.

#### 4. Monthly Picture Tests

**Files**:

- `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs`
- `src/HouseholdBudgetMate.Tests/Tests/Services/IncomeServiceTests.cs` if focused balance-base coverage is cheaper there

**Intent**: Use the accepted S-02 monthly loop as the numeric oracle for the new DTO.

**Contract**: Assert that the new monthly picture reports the same final live balance `7075.00`, plan remaining `800.00`, due savings `300.00`, future savings context `600.00`, closed/reopened state, and incomplete-balance behavior already protected by existing tests.

#### 5. Existing Projection Compatibility

**Files**:

- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs`
- `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`

**Intent**: Ensure adding the new boundary does not break existing Plan/Home/Accounts/Statistics responsibilities.

**Contract**: Existing methods remain available. Statistics still does not become a `Live balance` screen unless a separate product decision changes that.

### Success Criteria:

#### Automated Verification:

- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~IncomeServiceTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- `dotnet build HouseholdBudgetMate.slnx -c Release`
- `git diff --check -- .`

#### Manual Verification:

- Review `MonthlyFinancialPictureDto` and confirm it reads as the accepted monthly contract, not a resurrected `Safe-to-spend`.
- Compare a known month through existing Plan/Accounts UI and confirm values still match the new monthly picture tests.

**Implementation Note**: Pause after this phase. The public DTO shape is the most important review point because later phases will build on it.

---

## Phase 2: Effective Actual and Restore Hardening

### Overview

Centralize effective actual semantics and remove known drift risks without changing schema or existing final-line-item deletion behavior. This phase also rejects negative line-item amounts until a refund/correction feature is deliberately designed.

### Changes Required:

#### 1. Effective Actual Policy

**Files**:

- `src/HouseholdBudgetMate.Application/Helpers/ExpenseActualAmountCalculator.cs`
- `src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs`
- `src/HouseholdBudgetMate.Application/Services/IncomeService.cs`
- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs`

**Intent**: Make effective actual amount a single application policy for projections that need actual spending.

**Contract**: Preserve the current rule: when an expense has line items, effective actual is the line-item sum; otherwise it is parent `Expense.ActualAmount`. The final-line-item deletion behavior remains unchanged and documented by tests.

#### 2. Live Balance and Monthly Projections Alignment

**Files**:

- `src/HouseholdBudgetMate.Application/Services/IncomeService.cs`
- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs`

**Intent**: Ensure monthly financial projections touched by this change do not disagree because one path uses DTO effective actual and another path uses stale parent actual.

**Contract**: `GetLiveBalanceAsync` and `GetMonthlyFinancialPictureAsync` must calculate expense totals with the same effective-actual semantics. Statistics/dashboard paths should be evaluated and updated only where research shows they participate in the same monthly picture contract.

#### 3. Line-Item Amount Validation

**File**: `src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs`

**Intent**: Reject negative line-item amounts at the service boundary.

**Contract**: Create and update line-item validators must require amount to be zero or greater. Error messages should match the tone of existing expense amount validation. No refund semantics are introduced.

#### 4. Backup Restore Recalculation

**Files**:

- `src/HouseholdBudgetMate.Application/Services/Backup/BackupRestoreExecutor.cs`
- Backup restore tests under `src/HouseholdBudgetMate.Tests/Tests/Services/BackupServiceTests.cs`

**Intent**: Prevent restored expenses with line items from drifting between Plan DTOs and live balance.

**Contract**: After restoring line items, recalculate parent `Expense.ActualAmount` for affected expenses using the same effective actual policy. This recalculation is part of restore/import mutation, not a global migration of existing data.

#### 5. Drift Diagnostic

**Files**:

- Prefer test coverage in `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs` or backup tests
- Add a small internal diagnostic helper only if implementation needs one

**Intent**: Surface potential parent actual vs line-item drift without automatically rewriting existing data.

**Contract**: The implementation should include a deterministic test or diagnostic path proving drift can be detected. It must not perform an automatic production-wide recalculation.

### Success Criteria:

#### Automated Verification:

- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~IncomeServiceTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~BackupServiceTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- `dotnet build HouseholdBudgetMate.slnx -c Release`
- `git diff --check -- .`

#### Manual Verification:

- Review that no migration or EF snapshot changed.
- Review backup restore behavior with a backup containing expenses plus line items and confirm parent actual is recalculated after restore.
- Confirm final-line-item deletion tests still assert the preserved current behavior.

**Implementation Note**: If negative line items are discovered to be real refund data in current usage, stop and split that into a refund/correction design change instead of silently accepting or rejecting it here.

---

## Phase 3: UI Alignment, Recurring Documentation, and Evidence

### Overview

Adopt the monthly picture where it improves UI consistency, align closed-month affordances, document recurring generated-row semantics, and collect final verification evidence. This phase closes the loop without expanding into unrelated domain cleanup.

### Changes Required:

#### 1. Incremental UI Adoption

**Files**:

- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs`
- `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs` or related Accounts code only if it consumes the full monthly picture
- `src/HouseholdBudgetMate.Web/Components/Pages/Home.razor` or Dashboard-related code only if it benefits from the full monthly picture

**Intent**: Use `MonthlyFinancialPictureDto` in places that currently stitch together plan and live-balance state, while keeping screen-specific responsibilities intact.

**Contract**: Plan and Accounts may consume the new DTO if it reduces duplication. Statistics must not be forced to show `Live balance`. Existing labels and accepted monthly values stay unchanged.

#### 2. Closed-Month UI Affordances

**Files**:

- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor`
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.SavingsTransfers.cs`
- Nearby PlanPage partials if they expose write actions while closed

**Intent**: Make visible UI affordances match service-level closed-month read-only behavior.

**Contract**: Savings transfer create/edit/delete controls and any adjacent touched write actions must be visibly disabled or blocked in the same way as existing expense/income/line-item actions. Service guards remain authoritative.

#### 3. Recurring Generated-Row Documentation and Tests

**Files**:

- `context/changes/domain-refactor/recurring-semantics.md`
- Existing recurring tests in `ExpenseServiceTests.cs` / `IncomeServiceTests.cs` if direct coverage is missing

**Intent**: Preserve and document the current rule that soft-deleted generated recurring rows block automatic regeneration.

**Contract**: Document that `IgnoreQueryFilters()` duplicate checks are intentional. Add direct tests for `AddRegularExpenseDefinitionToMonthAsync` returning false on duplicates if absent and cheap to add.

#### 4. UI and Contract Tests

**Files**:

- `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`
- `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopRenderedTests.cs`
- Service tests touched by the UI adoption

**Intent**: Protect the new public DTO and closed-month UI affordances without brittle markup overreach.

**Contract**: Tests should assert that `Safe-to-spend` remains absent, accepted labels remain, closed-month savings-transfer affordances are represented, and the monthly picture contract is reachable from the intended service.

#### 5. Acceptance Evidence

**File**: `context/changes/domain-refactor/acceptance-evidence.md`

**Intent**: Record final verification and manual/browser notes before implementation is considered complete.

**Contract**: Evidence must include commands run, results, date, any skipped checks with reason, and manual notes for Plan monthly picture, Accounts live balance/incomplete guidance, line-item actual behavior, backup restore recalculation, and closed-month savings-transfer affordances.

### Success Criteria:

#### Automated Verification:

- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~MonthlyBudgetingLoopRenderedTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~IncomeServiceTests|FullyQualifiedName~BackupServiceTests|FullyQualifiedName~AccountServiceTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`
- `dotnet build HouseholdBudgetMate.slnx -c Release`
- `git diff --check -- .`

#### Manual Verification:

- Start the app and inspect a known monthly scenario on Plan and Accounts.
- Confirm `Live balance`, `Pozostalo w planie`, savings transfer timing, and incomplete-balance guidance still match accepted behavior.
- Confirm closed-month savings-transfer actions are visibly non-editable and still blocked by service guards.
- Restore a backup with line items and confirm restored monthly totals agree across Plan and live balance.
- Review `acceptance-evidence.md` before archiving.

**Implementation Note**: This phase may include browser verification if a local app is running. Do not block completion on Playwright unless auth state and server setup are already prepared; record skipped browser automation explicitly.

---

## Testing Strategy

### Unit and Policy Tests:

- Effective actual policy: parent actual vs line-item sum, parent actual ignored when line items exist, final-line-item deletion preserved.
- Line-item validation: negative create/update amounts rejected.
- Recurring generated rows: duplicate generated rows remain blocked, including soft-deleted rows where current behavior uses `IgnoreQueryFilters()`.

### Service Integration Tests:

- `MonthlyBudgetingLoopTests` remains the core numeric oracle for the accepted monthly picture.
- `IncomeServiceTests` continues to protect live balance formula and missing-vs-zero balance-base behavior.
- `ExpenseServiceTests` protects plan KPI and effective actual behavior.
- `BackupServiceTests` protects restore recalculation after line-item import.

### UI Contract Tests:

- Static/rendered UI tests protect labels, no `Safe-to-spend` reintroduction, closed-month affordances, and service wiring.
- Avoid broad brittle markup assertions; prefer behavior-relevant labels, method names, disabled states, and DTO/service contract usage.

### Manual Testing Steps:

1. Open an existing month and confirm Plan values match `MonthlyFinancialPictureDto` expectations.
2. Confirm Accounts live balance and incomplete-balance guidance still handle missing previous balance and saved zero correctly.
3. Add/edit/delete a line item and confirm Plan, live balance, and monthly picture totals agree.
4. Delete the final line item and confirm the preserved parent actual behavior.
5. Try a negative line-item amount and confirm validation blocks it.
6. Restore backup data with line items and confirm parent actual totals are recalculated.
7. Close a month and confirm savings-transfer controls are visibly disabled or non-editable.
8. Confirm no `Safe-to-spend` wording appears in the touched UI.

## Performance Considerations

The new monthly picture may initially compose existing `GetMonthAsync` and `GetLiveBalanceAsync` behavior. That is acceptable for correctness, but avoid unnecessary duplicate database work when the implementation can share loaded snapshots safely. Performance optimization must not change month creation/sync side effects or live-balance completeness rules.

If the final implementation adds a new read method used by multiple screens, watch for repeated calls from `PlanPage.LoadAsync`. Prefer one monthly picture load per selected month where it replaces separate plan/live-balance reads.

## Migration Notes

No database migration is planned. Adding DTOs and service methods changes public code contracts but not persisted schema. Existing financial data should not be automatically recalculated. Restore/import mutation may recalculate parent actual for restored expenses because that data is being written as part of restore.

If implementation discovers widespread existing drift between parent actual and line-item sums, record it in `acceptance-evidence.md` and open a separate data repair change.

## Risks and Assumptions

- The public DTO shape may need review after Phase 1 before UI adoption.
- Some projections may intentionally use persisted parent actual outside the monthly picture; update only paths that participate in the accepted monthly contract.
- Existing users may have negative line-item data only if they bypassed current UI/service assumptions; this plan treats that as invalid until proven otherwise.
- Backup restore recalculation may affect restored audit/timestamp shape; tests should focus on restored financial correctness and avoid overfitting incidental timestamps.
- Closed-month UI alignment should stay minimal and not become a full UX redesign.

## References

- Research: `context/changes/domain-refactor/research.md`
- Distillation: `context/domain/01-domain-distillation.md`
- Monthly plan read model: `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:382`
- Plan KPI calculation: `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2492`
- Live balance calculation: `src/HouseholdBudgetMate.Application/Services/IncomeService.cs:399`
- Live balance expense total: `src/HouseholdBudgetMate.Application/Services/IncomeService.cs:480`
- Account balance upsert: `src/HouseholdBudgetMate.Application/Services/AccountService.cs:182`
- Effective actual helper: `src/HouseholdBudgetMate.Application/Helpers/ExpenseActualAmountCalculator.cs:7`
- Backup restore expense/line item path: `src/HouseholdBudgetMate.Application/Services/Backup/BackupRestoreExecutor.cs:316`
- Line-item validation: `src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:272`
- Account balance unique index: `src/HouseholdBudgetMate.Domain/EntityConfiguration/AccountMonthBalanceConfiguration.cs:30`
- Regular expense generated-row unique index: `src/HouseholdBudgetMate.Domain/EntityConfiguration/ExpenseConfiguration.cs:67`
- Regular income generated-row unique index: `src/HouseholdBudgetMate.Domain/EntityConfiguration/IncomeConfiguration.cs:56`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append `- <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Monthly Financial Picture Boundary

#### Automated

- [x] 1.1 `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"` - 285b07b
- [x] 1.2 `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~IncomeServiceTests"` - 285b07b
- [x] 1.3 `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"` - 285b07b
- [x] 1.4 `dotnet build HouseholdBudgetMate.slnx -c Release` - 285b07b
- [x] 1.5 `git diff --check -- .` - 285b07b

#### Manual

- [x] 1.6 Review `MonthlyFinancialPictureDto` and confirm it reads as the accepted monthly contract, not a resurrected `Safe-to-spend` - 285b07b
- [x] 1.7 Compare a known month through existing Plan/Accounts UI and confirm values still match the new monthly picture tests - 285b07b

### Phase 2: Effective Actual and Restore Hardening

#### Automated

- [x] 2.1 `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests"` - 285b07b
- [x] 2.2 `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~IncomeServiceTests"` - 285b07b
- [x] 2.3 `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~BackupServiceTests"` - 285b07b
- [x] 2.4 `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"` - 285b07b
- [x] 2.5 `dotnet build HouseholdBudgetMate.slnx -c Release` - 285b07b
- [x] 2.6 `git diff --check -- .` - 285b07b

#### Manual

- [x] 2.7 Review that no migration or EF snapshot changed - 285b07b
- [x] 2.8 Review backup restore behavior with a backup containing expenses plus line items and confirm parent actual is recalculated after restore - 285b07b
- [x] 2.9 Confirm final-line-item deletion tests still assert the preserved current behavior - 285b07b

### Phase 3: UI Alignment, Recurring Documentation, and Evidence

#### Automated

- [x] 3.1 `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~MonthlyBudgetingLoopRenderedTests"` - 285b07b
- [x] 3.2 `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~IncomeServiceTests|FullyQualifiedName~BackupServiceTests|FullyQualifiedName~AccountServiceTests"` - 285b07b
- [x] 3.3 `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release` - 285b07b
- [x] 3.4 `dotnet build HouseholdBudgetMate.slnx -c Release` - 285b07b
- [x] 3.5 `git diff --check -- .` - 285b07b

#### Manual

- [x] 3.6 Start the app and inspect a known monthly scenario on Plan and Accounts - 285b07b
- [x] 3.7 Confirm `Live balance`, `Pozostalo w planie`, savings transfer timing, and incomplete-balance guidance still match accepted behavior - 285b07b
- [x] 3.8 Confirm closed-month savings-transfer actions are visibly non-editable and still blocked by service guards - 285b07b
- [x] 3.9 Restore a backup with line items and confirm restored monthly totals agree across Plan and live balance - 285b07b
- [x] 3.10 Review `acceptance-evidence.md` before archiving - 285b07b
