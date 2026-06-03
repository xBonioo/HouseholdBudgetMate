# Align Safe-to-Spend Contract - Plan Brief

> Full plan: `context/changes/align-safe-to-spend-contract/plan.md`

> 2026-05-30 supersession: this plan brief is historical. The MVP no longer includes a separate `Safe-to-spend` output. Current acceptance is `Live balance`, `Pozostało w planie`, savings context, and incomplete-balance guidance in `context/changes/verify-monthly-safe-to-spend-loop/`.

## What & Why

The MVP needs two trustworthy but distinct monthly indicators: current liquidity and the amount safe to spend after planned commitments are protected. Today the application shows expense-plan remainder and `Live balance` without a contract that explains which one fulfills the product promise, and a missing prior-month account balance can make `Live balance` look valid while being incomplete.

## Starting Point

`IncomeService.GetLiveBalanceAsync` already aggregates prior non-savings account balances, due incomes, actual expenses, and due savings transfers. `ExpenseService` separately supplies the `Pozostało` plan KPI; Plan, Dashboard, and Accounts display these values with overlapping labels and no consistent missing-base warning.

## Desired End State

`Live balance` clearly reports cash available based on prior closing balances and dated current-month movements. `Safe-to-spend` reports that liquidity after reserving all outstanding planned expenses and planned savings transfers not yet executed. When prior closing balances are missing, both outputs are visibly incomplete rather than rendered as trustworthy amounts.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Product outputs | Keep both `Live balance` and `Safe-to-spend` | Liquidity and discretionary safety answer different user questions. |
| Account balance meaning | Monthly balance is closing balance | A closed month provides the starting base for the next month. |
| Income recognition | Count income when expected date is reached | Retains the selected current model for MVP. |
| Planned commitments | Reserve all remaining positive planned expenses | The user should not spend money already budgeted for later costs. |
| Savings handling | Due transfers reduce live balance; future planned transfers reduce safe-to-spend | Avoids treating reserved savings as spendable without distorting current cash. |
| Missing base balance | Mark values incomplete and require prior closing input | A visible unknown is safer than a misleading zero-based result. |
| Change scope | Contract, service calculation, tests, and UI presentation | `S-02` needs a user-visible, verifiable contract rather than documentation alone. |

## Scope

**In scope:**

- Extend the financial-result DTO contract with safe-to-spend, reserve breakdown, and completeness state.
- Implement the agreed formulas in the existing monthly financial aggregation service.
- Add test coverage for reserve rules and the reported missing-base failure mode.
- Align Plan, Dashboard, and Accounts presentation and domain/product documentation.

**Out of scope:**

- Income receipt confirmation; income remains date-based for MVP.
- Changes to closing-balance storage semantics or savings-account inclusion.
- Broader PIN validation, deployment readiness, or end-to-end `S-02` acceptance.
- New automated UI-test infrastructure.

## Architecture / Approach

Keep `MonthPlanKpiDto.RemainingTotal` as expense-plan progress. Extend `LiveBalanceDto` and `IncomeService.GetLiveBalanceAsync` as the shared financial-result contract for liquidity, safe-to-spend, reserve details, and completeness, then have the existing three UI consumers render the same definitions and corrective guidance.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Contract and completeness boundary | DTO and documentation define both results and missing-base behavior | Ambiguous terminology can survive into implementation |
| 2. Calculation and service verification | Authoritative formulas and regression tests | Double-counting reserved expenses or savings |
| 3. Presentation and user-flow verification | Consistent UI values, labels, and incomplete-data guidance | One screen may continue to imply the wrong meaning |

**Prerequisites:** Existing `F-01` change identity and accepted decisions from this planning session; no upstream implementation dependency.
**Estimated effort:** Approximately 2-3 focused sessions across 3 phases.

## Open Risks & Assumptions

- Income is intentionally counted by expected date, not actual receipt; a delayed income can still overstate both indicators within MVP semantics.
- The approved contract assumes every positive planned expense should reserve money, including optional planned spending.
- UI validation is manual because the repository currently lacks component or browser-test infrastructure.

## Success Criteria (Summary)

- The application displays distinct, consistently defined `Live balance`, `Safe-to-spend`, and plan remainder values.
- Automated service tests prove reserve handling, dated savings behavior, and missing prior-balance completeness.
- Users are told to supply a prior closing balance whenever financial outputs cannot yet be considered reliable.
