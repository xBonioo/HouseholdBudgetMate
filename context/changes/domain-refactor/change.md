---
change_id: domain-refactor
title: Refactor domain layer around monthly reconciliation
status: implemented
created: 2026-06-14
updated: 2026-06-14
archived_at: null
---

## Notes

Seeded from `context/domain/01-domain-distillation.md`.

Prepare a feature for repairing the domain layer and shaping the refactor proposals from the domain distillation. Primary focus should be the highest-ranked refactor candidate: `MonthlyFinancialPicture` / `MonthPlan` as the monthly reconciliation boundary, including `Live balance`, complete balance base, `Pozostalo w planie`, savings transfers, and read-only closed-month rules.

Secondary candidates to evaluate during research/planning:

- `Expense` with `ExpenseLineItems`, especially the invariant that actual amount equals the line-item sum when line items exist.
- `AccountBalanceBase` / `AccountMonthBalance`, including a single balance row per account-month and explicit distinction between saved zero and missing balance.
- `RecurringPlanSource` / regular definitions, including idempotent generation and duplicate prevention.
- `HouseholdAccessProfile`, `CategoryEnvelope`, and `AuditTrail` only as supporting areas unless research shows they block the monthly reconciliation refactor.

Planning should preserve the existing architecture constraint that UI calls application services, application services own workflows, and domain entities are currently persistence-oriented unless the plan deliberately proposes a staged change to that boundary.
