# Refactor Opportunities - Plan Brief

> Full plan: `context/changes/refactor-opportunities/plan.md`
> Research: `context/changes/refactor-opportunities/research.md`

## What & Why

This plan turns the research ranking into a staged refactor roadmap. The first priority is to clean up `PlanPage` post-save orchestration without changing behavior; later slices pin line-item effective actual amount semantics and save-boundary guardrails.

## Starting Point

`PlanPage` currently repeats a family of save flows across expenses, incomes, savings transfers, and line items. The repetition is real, but full `LoadAsync` reloads are also the current consistency boundary for month plan, dashboard summary, incomes, live balance, chart/KPI state, and dirty tracking.

## Desired End State

Save-like handlers use named refresh modes instead of ad hoc repeated ceremony. The first refactor keeps full reload behavior and current API contracts intact. Later phases make line-item actual amount semantics and save/audit boundaries explicit enough to support future optimization safely.

## Key Decisions Made

| Decision | Choice | Why | Source |
|---|---|---|---|
| Plan scope | Multi-slice roadmap | User selected broader scope than a single helper, including later line-item/save-boundary work. | Plan |
| First implementation value | Inventory and guardrails first | Refactor touches a hot path with subtle variants, so behavior evidence comes before code movement. | Research / Plan |
| Reload strategy | Preserve full reload | `LoadAsync` updates multiple dependent projections, so local DTO patching is deferred. | Research / Plan |
| First handler family | Expenses first | Expenses are the highest-value hot path and carry the most important special cases. | Plan |
| Line-item semantics | Treat as later dedicated slice | Protect the rule first, then centralize without schema changes. | Research / Plan |
| Save boundaries | Guard touched paths | Audit/timestamp behavior is load-bearing, but a full save matrix would be overkill. | Research / Plan |
| Browser evidence | Full smoke at the end | Playwright/manual browser evidence verifies runtime behavior after deterministic guards pass. | Plan |

## Scope

**In scope:**

- Save-handler inventory for `PlanPage`.
- Static and service guardrails before refactor.
- Expenses-first local post-save orchestration helper.
- Extending the pattern to incomes, savings transfers, and line items.
- No-schema-change effective actual amount cleanup.
- Targeted audit/user-scope guardrails for touched paths.
- Browser smoke evidence recorded in `acceptance-evidence.md`.

**Out of scope:**

- Replacing full reload with local optimistic DTO patching.
- Changing `IExpenseService`, DTO contracts, domain entities, EF config, migrations, or snapshots in the first `PlanPage` slices.
- Changing line-item business rules.
- Renaming `Expense` to `Post` or introducing a new `wpis` aggregate.
- Adding MediatR/CQRS or a broad UI command pipeline.

## Architecture / Approach

Keep the existing simple architecture: `PlanPage` builds request DTOs and calls application services directly; services keep business rules and persistence. The refactor is local first: name refresh modes inside `PlanPage`, preserve `LoadAsync`, then only later touch application-layer actual amount helpers if tests pin current behavior.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Baseline Inventory And Guardrails | Inventory plus static/service/audit guardrails | Missing a current refresh exception |
| 2. Expenses-First Post-Save Orchestration | Local helper applied to expense flows | Breaking copy/suggestion/reorder semantics |
| 3. Extend Pattern To Remaining PlanPage Saves | Incomes, savings, and line items use the pattern | Losing line-item re-expand behavior |
| 4. Effective Actual Amount Slice | Test-first no-schema cleanup around effective actual | Accidentally changing business semantics |
| 5. Save-Boundary Guardrails And Browser Evidence | Audit/user-scope checks plus browser smoke evidence | Runtime UI regression missed by static tests |

**Prerequisites:** completed research at `context/changes/refactor-opportunities/research.md`.
**Estimated effort:** about 4-5 focused implementation sessions across 5 phases.

## Open Risks & Assumptions

- Full reload remains acceptable for this refactor; performance optimization is a separate future change.
- Final-line-item deletion behavior should preserve current implementation unless Phase 1 tests expose a mismatch between docs and code.
- Playwright smoke depends on a running app at `https://localhost:7135/` and prepared auth state at `playwright/.auth/user.json`.
- No schema change should be needed; discovering one stops this plan and requires a separate migration-focused change.

## Success Criteria (Summary)

- `PlanPage` save handlers share named orchestration while preserving all current refresh modes.
- Line-item actual amount and touched save/audit behavior are protected by targeted tests.
- Final browser smoke evidence confirms monthly save flows still work after refactor.
