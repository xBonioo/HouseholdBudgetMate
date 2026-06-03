# Align Safe-to-Spend Contract Implementation Plan

> 2026-05-30 supersession: this plan is historical. The MVP no longer includes a separate `Safe-to-spend` output. Current acceptance is `Live balance`, `Pozostało w planie`, savings context, and incomplete-balance guidance in `context/changes/verify-monthly-safe-to-spend-loop/`.

## Overview

Define and implement the MVP financial-result contract so a household member can distinguish current liquidity from money that remains safe to spend after planned commitments. The change introduces a trustworthy `Safe-to-spend` output alongside `Live balance`, fixes misleading behavior when prior-month account balances are absent, and aligns the primary UI surfaces and verification evidence.

## Current State Analysis

The application already renders three related but different financial concepts:

- The plan KPI `RemainingTotal` reports expense-plan remainder and is currently labelled `Pozostało`.
- `LiveBalanceDto.CurrentBalance` reports a cash-position calculation based on prior non-savings account balances, due income, actual expenses, and due savings transfers.
- Dashboard savings figures report balance deltas and are not interchangeable with either value above.

The PRD requires one trustworthy safe-to-spend/saving view, but the existing UI displays `Pozostało` and `Live balance` without defining which one satisfies that promise. The user has also reported that `Live balance` behaves incorrectly in the application. Code inspection identifies a concrete gap: account balances are entered as monthly `ClosingBalance` values, while the live calculation only uses balances from a previous month; if no prior-month closing balance exists, the current result silently uses zero rather than disclosing an incomplete base.

## Desired End State

The application exposes two explicitly distinct current-month values:

- `Live balance`: cash available from the closing balance in the calendar month immediately preceding the selected month, plus income whose expected date has been reached, minus actual expenses and savings transfers whose transfer date has been reached.
- `Safe-to-spend`: `Live balance` minus the outstanding remainder of all positive planned expenses and minus planned savings transfers not yet reflected in `Live balance`.

`Pozostało` remains an expense-plan progress value and is not presented as the product's safe spending figure. If required prior-month non-savings account balances are unavailable, both `Live balance` and `Safe-to-spend` show an incomplete-data state and direct the user to enter a base balance rather than presenting a trustworthy amount. Completion is verified through service tests and a manual pass through Plan, Dashboard, and Accounts screens.

### Key Discoveries:

- [IncomeService.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Application/Services/IncomeService.cs:399) already centralizes account, income, actual-expense, and dated savings-transfer aggregation in `GetLiveBalanceAsync`, making it the natural home for the expanded financial-result contract.
- [ExpenseService.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2020) calculates `RemainingTotal` only from expense rows; this output must remain separate from safe-to-spend semantics.
- [PlanPage.razor](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:104), [Home.razor](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Components/Pages/Home.razor:103), and [Accounts.razor](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor:79) present overlapping monetary figures and must use consistent labels and completeness messaging.
- [AccountService.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Application/Services/AccountService.cs:164) persists monthly closing balances, while [IncomeService.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Application/Services/IncomeService.cs:405) bases the selected month on prior closing balances. Missing prior data is therefore a valid incomplete-input state, not a zero balance.
- [IncomeServiceTests.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Tests/Tests/Services/IncomeServiceTests.cs:1047) and [ExpenseServiceTests.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:452) provide established service-test patterns; no component or browser-test infrastructure was found.

## What We're NOT Doing

- Adding income receipt confirmation or replacing the accepted expected-date rule for income recognition.
- Changing monthly account entries from closing-balance semantics to opening-balance or current-balance semantics.
- Including savings-account balances in `Live balance` or `Safe-to-spend`.
- Introducing new handling for loans beyond their existing expense rows and actual payments.
- Adding database migrations solely for the new calculation contract unless implementation research reveals a contract field cannot be represented in DTOs.
- Building automated Blazor component or end-to-end UI test infrastructure in this change.
- Completing the broader `S-02` full monthly-loop acceptance verification or PIN-gated access work.

## Implementation Approach

Extend the existing financial aggregation response returned by `IIncomeService.GetLiveBalanceAsync` so one service query supplies both liquidity and safe-to-spend semantics, along with an explicit completeness state for the required account-balance base. Keep `MonthPlanKpiDto` focused on planned-versus-spent budget progress. Update all surfaces that consume `LiveBalanceDto` to label values consistently and provide actionable incomplete-data messaging. Document the agreed contract so later roadmap validation uses the same rule.

## Critical Implementation Details

### State Sequencing

`Safe-to-spend` must not double-count amounts already deducted from `Live balance`: actual expense amounts are deducted through `Live balance`, so the commitment reserve includes only outstanding planned expense remainder; due savings transfers already deducted from `Live balance` must not also be included in the future savings reserve.

### User Experience Spec

When the balance basis is incomplete, monetary values must not be rendered as trustworthy zero-value KPIs. Plan, Dashboard, and Accounts surfaces should consistently signal that the previous-month closing balance is required before the current-month liquidity and safe-to-spend outputs are reliable.
An explicitly saved closing-balance entry of `0` is valid complete input; the Accounts surface must distinguish it from an unsaved/missing entry that happens to render an editable zero default.
This completeness requirement applies to open months. A closed historical month is read-only and must calculate from the latest prior balances already stored in the database without requiring retroactive data repair.

Archived accounts are included in the balance-base requirement only for selected months completed before they were archived. An account archived during the selected month is not required to have that month's closing balance. For legacy archived records without `ArchivedAtUtc`, `UpdatedAtUtc` is the best available archive timestamp.

## Phase 1: Establish Financial Result Contract and Completeness Boundary

### Overview

Introduce the contract vocabulary and payload needed to represent both approved values and the missing-base state, while aligning written domain definitions with the implemented monthly-balance model.

### Changes Required:

#### 1. Financial Result DTO Contract

**File**: `src/HouseholdBudgetMate.Abstractions/Contracts/Incomes/Dto/LiveBalanceDto.cs`

**Intent**: Extend the existing aggregation result so callers receive `Safe-to-spend`, its reserve components, and an explicit signal that the current calculation lacks sufficient prior closing-balance data.

**Contract**: Preserve existing `CurrentBalance` as the `Live balance` numeric value and add stable DTO members for the safe-to-spend amount, outstanding planned-expense reserve, pending savings-transfer reserve, and balance-base completeness/message data required by the UI.

#### 2. Domain Contract Documentation

**File**: `docs/DOMAIN.md`

**Intent**: Replace the ambiguous live-balance-only description with the agreed distinction between closing-balance inputs, liquidity, expense-plan remainder, and safe-to-spend.

**Contract**: The `Live balance` section documents prior-month non-savings closing-balance semantics, expected-date income recognition, actual-expense subtraction, due savings-transfer subtraction, `Safe-to-spend` reservation rules, and incomplete-state behavior when required base balances are absent.

#### 3. Product Requirement Lineage

**File**: `context/foundation/prd.md`

**Intent**: Record how FR-007 is concretely fulfilled so downstream `S-02` validates the selected product contract rather than treating any displayed remainder as sufficient.

**Contract**: Clarify in the safe-to-spend business-logic wording that MVP presents both current liquidity and reserved safe-to-spend, while planned expense remainder remains supporting information.

### Success Criteria:

#### Automated Verification:

- The solution compiles after contract changes: `dotnet build HouseholdBudgetMate.slnx`
- Existing test suite still compiles and passes against the expanded DTO contract: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj`

#### Manual Verification:

- Review the documented formulas against the approved decisions: two indicators, closing-balance basis, expected-date income rule, all planned expense reserves, pending savings reserves, and incomplete-data behavior.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the contract wording matches the intended household budgeting rule before proceeding to calculation work.

---

## Phase 2: Implement Calculation and Service Verification

### Overview

Make the application service return the approved values and expose incomplete input rather than silently producing an apparently valid zero-based result.

### Changes Required:

#### 1. Current-Month Financial Aggregation

**File**: `src/HouseholdBudgetMate.Application/Services/IncomeService.cs`

**Intent**: Expand `GetLiveBalanceAsync` from cash-position aggregation into the authoritative financial-result query consumed across the application.

**Contract**: Retain the accepted `Live balance` formula:
`prior non-savings closing balances + incomes with ExpectedDayOfMonth <= today - actual expenses - savings transfers with TransferDate <= today`.
Add `Safe-to-spend` as:
`Live balance - outstanding positive planned-expense remainder - savings transfers with TransferDate > today`.
An outstanding planned-expense remainder is the non-negative unpaid portion of every expense with `PlannedAmount > 0`; actual amounts already reduce live balance and must not be subtracted again beyond the remaining planned portion. The result identifies missing prior-month non-savings account balance inputs as incomplete.

#### 2. Calculation Test Matrix

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/IncomeServiceTests.cs`

**Intent**: Turn the approved formula and reported failure mode into durable executable evidence.

**Contract**: Add or update tests covering:
`Live balance` with prior-month closing balance and due income; `Safe-to-spend` reservation of remaining planned expense amounts; planned savings transfer reserved before its due date and deducted from live balance after its due date without double counting; unplanned actual expense deducted through live balance; expected-date income behavior retained; and missing prior closing balance returning incomplete status rather than a valid-looking calculation.

#### 3. Preserve Plan KPI Separation

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs`

**Intent**: Prevent future code from collapsing the existing plan remainder into the newly authoritative safe-to-spend value.

**Contract**: Keep or strengthen assertions showing `MonthPlanKpiDto.RemainingTotal` remains derived from expense-plan behavior only and does not absorb account balances, incomes, or savings reservations.

### Success Criteria:

#### Automated Verification:

- Targeted aggregation and KPI tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~IncomeServiceTests|FullyQualifiedName~ExpenseServiceTests"`
- Full test suite passes without financial-contract regressions: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj`

#### Manual Verification:

- Inspect representative calculations for a month with prior balance, due income, paid expense, remaining planned expense, and future savings transfer; confirm `Live balance` and `Safe-to-spend` differ exactly by reserved future commitments.
- Confirm a month lacking the required prior closing balance is returned as incomplete and does not invite interpretation of a numeric result as reliable.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that sample financial scenarios produce credible results before proceeding to UI changes.

---

## Phase 3: Present Both Indicators and Verify User Flow

### Overview

Expose the approved contract in user-facing screens so the household member can tell available cash from safe discretionary capacity and can fix missing base data.

### Changes Required:

#### 1. Monthly Plan Presentation

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor`

**Intent**: Make the plan screen's financial KPIs semantically clear during the core expense-entry workflow.

**Contract**: Keep `Pozostało` identified as plan remainder, present both `Live balance` and `Safe-to-spend` with accurate component explanations, include savings-transfer effects in the live-balance description, and replace reliable-looking amounts with incomplete-data guidance when the DTO marks the base as missing.

#### 2. Monthly Plan State Binding

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.cs`

**Intent**: Provide the presentation model/state helpers needed for the new indicator and incomplete status without moving calculation logic into the component.

**Contract**: Continue consuming `LiveBalanceDto` from `IncomeService`; component helpers may format or branch on completeness but must not recalculate financial values.

#### 3. Dashboard Presentation

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/Home.razor`

**Intent**: Ensure the at-a-glance screen advertises the same two values and definitions used in monthly planning.

**Contract**: Surface `Safe-to-spend` alongside `Live balance`, distinguish existing expense-plan `Pozostało`, show the relevant reserve or incomplete-state explanation, and retain the existing service-fetch pattern.

#### 4. Accounts Liquidity Presentation

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor`

**Intent**: Prevent the account screen from describing an incomplete or commitment-free amount as generally “available”.

**Contract**: Display `Live balance` and `Safe-to-spend` consistently with the other screens, update the current “available after movements” wording, and guide the user to enter the preceding month's closing balances when the selected month has no complete base.

#### 5. Accounts Presentation Model

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs`

**Intent**: Carry the service-provided safe-to-spend and completeness fields into the accounts KPI model.

**Contract**: Extend `AccountsOverviewModel` and its mapping from `LiveBalanceDto`; do not infer missing balance completeness from editable UI input or locally recompute either financial result.

### Success Criteria:

#### Automated Verification:

- The web and dependent projects compile with the expanded presentation contract: `dotnet build HouseholdBudgetMate.slnx`
- The full service/architecture test suite remains green: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj`

#### Manual Verification:

- On the month plan screen, create or use a month with a previous closing balance, a due income, actual spend, a partially unspent planned expense, and a future savings transfer; verify both values and their labels match the approved formulas.
- On Dashboard and Accounts, verify the same month exposes consistent `Live balance` and `Safe-to-spend` values and does not present `Pozostało` as the safe-spending amount.
- Select a month without required prior closing balances and verify all three screens clearly mark financial results as incomplete and direct the user to provide the missing base balance.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the user-facing monthly flow is credible and consistently labelled.

---

## Testing Strategy

### Unit Tests:

- Extend `IncomeServiceTests` as the primary executable contract for both monetary outputs and balance-base completeness.
- Retain focused `ExpenseServiceTests` assertions that protect the separate plan-remainder KPI semantics.
- Reuse the existing deterministic `StaticDateTimeProvider` test pattern so due versus future income/transfer cases remain stable.

### Integration Tests:

- No new browser-test or Blazor component-test framework is introduced in this change.
- Use full application-project compilation and the existing service-test suite as automated integration coverage across contracts, services, and consuming project references.

### Manual Testing Steps:

1. Enter prior-month closing balances for at least one non-savings account, open the next month, add a dated income, planned and actual expense values, and a future savings transfer.
2. Confirm `Live balance` reflects cash movement by date and `Safe-to-spend` additionally reserves outstanding plan and future savings amounts.
3. Confirm a due savings transfer moves from reserved-only impact into `Live balance` impact without changing the total safe-to-spend reduction twice.
4. Confirm Plan, Dashboard, and Accounts use consistent labels and explanations.
5. Remove or avoid the prior-month balance input and confirm the app shows an incomplete result state with corrective guidance.

## Performance Considerations

The calculation remains a monthly aggregation at small-household scale, matching the PRD's low-volume target. The implementation should extend the existing service query path rather than introduce per-component calculations or additional UI-triggered request cascades; no caching or denormalized stored result is required.

## Migration Notes

No schema migration is planned. Existing monthly account values remain `ClosingBalance` records; the implementation clarifies that an open selected month's liquidity requires the closing balance from the immediately preceding calendar month and requires visible incomplete-state handling when it is absent. Closed historical months remain viewable from the latest previously stored balances without retroactive data-entry requirements. If implementation discovers that a completeness detail cannot be expressed through DTO additions alone, that discovery must be reviewed before adding database work.

## References

- Change identity: `context/changes/align-safe-to-spend-contract/change.md`
- Roadmap item: `context/foundation/roadmap.md` (`F-01`)
- Product requirements: `context/foundation/prd.md` (`FR-007`, `US-01`, `Business Logic`)
- Domain description: `docs/DOMAIN.md`
- Existing aggregation: `src/HouseholdBudgetMate.Application/Services/IncomeService.cs:399`
- Existing plan KPI: `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2020`
- Existing live-balance tests: `src/HouseholdBudgetMate.Tests/Tests/Services/IncomeServiceTests.cs:1047`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Establish Financial Result Contract and Completeness Boundary

#### Automated

- [x] 1.1 The solution compiles after contract changes: `dotnet build HouseholdBudgetMate.slnx` — 5a3ce88
- [x] 1.2 Existing test suite still compiles and passes against the expanded DTO contract: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj` — 5a3ce88

#### Manual

- [x] 1.3 Review the documented formulas against the approved financial decisions — 5a3ce88

### Phase 2: Implement Calculation and Service Verification

#### Automated

- [x] 2.1 Targeted aggregation and KPI tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~IncomeServiceTests|FullyQualifiedName~ExpenseServiceTests"` — 7301a99
- [x] 2.2 Full test suite passes without financial-contract regressions: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj` — 7301a99

#### Manual

- [x] 2.3 Inspect a representative complete-month calculation and confirm the two indicator values — 7301a99
- [x] 2.4 Confirm a month without required prior closing balance returns an incomplete result — 7301a99

### Phase 3: Present Both Indicators and Verify User Flow

#### Automated

- [x] 3.1 The web and dependent projects compile with the expanded presentation contract: `dotnet build HouseholdBudgetMate.slnx` — 16155c2
- [x] 3.2 The full service and architecture test suite remains green: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj` — 16155c2

#### Manual

- [x] 3.3 Verify both indicators and labels on the month plan screen with a representative month — 16155c2
- [x] 3.4 Verify consistent financial indicator presentation on Dashboard and Accounts — 16155c2
- [x] 3.5 Verify incomplete-data guidance on all three screens without prior closing balances — 16155c2
