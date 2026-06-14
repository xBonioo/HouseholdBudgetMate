# Domain Refactor - Plan Brief

> Full plan: `context/changes/domain-refactor/plan.md`
> Research: `context/changes/domain-refactor/research.md`

## What & Why

We are creating a named monthly reconciliation boundary, `MonthlyFinancialPicture`, so the app has one clear contract for `MonthPlan`, `Live balance`, `Pozostalo w planie`, savings timing, completeness guidance, and closed-month state. This repairs the domain layer by naming and centralizing the core financial picture without immediately rewriting EF entities.

## Starting Point

Today the accepted monthly picture is split across `ExpenseService`, `IncomeService`, `AccountService`, Blazor page state, and tests. Research confirmed the biggest issue is scattered rules, not missing DB constraints: account balances and generated recurring rows already have unique indexes.

## Desired End State

The app exposes a public `MonthlyFinancialPictureDto`, composed in Application, with existing monthly values preserved. Effective actual amount is centralized, backup restore recalculates parent actual after line-item import, negative line items are rejected, and closed-month UI controls stop implying editable savings transfers.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Architecture boundary | Application-layer builder/policy | Fits current service-owned workflow architecture. | Research / Plan |
| Public contract | New `MonthlyFinancialPictureDto` | Makes the monthly domain language visible to UI and tests. | Plan |
| Scope | Monthly core plus adjacent hardening | Covers real risks without expanding into full domain cleanup. | Research / Plan |
| Effective actual | Centralized policy, no schema change | Reduces projection drift while preserving behavior. | Research / Plan |
| Negative line items | Reject for now | Refund semantics are not designed. | Plan |
| Existing data | Diagnose, do not auto-migrate | Avoids silent financial data rewrites. | Plan |
| Phasing | Three phases | Creates reviewable slices and rollback points. | Plan |

## Scope

**In scope:**

- Public monthly picture DTO and service read contract.
- Application-layer monthly picture composition.
- Effective actual amount alignment for monthly projections.
- Negative line-item validation.
- Backup restore recalculation after line-item import.
- Minimal closed-month UI affordance alignment.
- Tests and acceptance evidence.

**Out of scope:**

- Rich aggregate rewrite of EF entities.
- Database migrations or automatic production data repair.
- Reintroducing `Safe-to-spend`.
- Envelopes, audit redesign, household access redesign, and loan engine redesign.
- Refund/correction semantics for negative line items.

## Architecture / Approach

The plan uses a strangler-style refactor: add the public DTO and application composer first, prove it matches existing `GetMonthAsync` + `GetLiveBalanceAsync`, then move effective actual and restore paths behind named policies. UI adoption is incremental and screen-specific; Statistics is not turned into a live-balance screen.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Monthly Financial Picture Boundary | Public DTO, service read method, application composer, S-02 oracle tests | DTO shape could overreach or accidentally revive `Safe-to-spend` |
| 2. Effective Actual and Restore Hardening | Centralized effective actual policy, negative validation, restore recalculation | Projection changes could alter financial totals |
| 3. UI Alignment, Recurring Documentation, and Evidence | Minimal closed-month UI alignment, recurring semantics docs/tests, final evidence | UI scope could expand beyond domain refactor |

**Prerequisites:** `context/changes/domain-refactor/research.md` remains the grounding source; no schema migration is expected.
**Estimated effort:** ~3 focused implementation sessions across 3 phases, plus manual verification for backup restore and closed-month UI.

## Open Risks & Assumptions

- The public DTO shape needs careful review after Phase 1.
- Some projections may intentionally remain outside the monthly picture and should not be changed casually.
- If real data contains negative line items, refund/correction design must split into a separate change.
- Existing parent actual drift is diagnosed, not automatically repaired.

## Success Criteria (Summary)

- `MonthlyFinancialPictureDto` reports the accepted S-02 monthly values without adding `Safe-to-spend`.
- Plan, live balance, line-item actuals, and backup restore agree for monthly spending totals.
- Closed months remain service-protected and visibly non-editable for touched savings-transfer actions.
