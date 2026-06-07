# Improve Monthly Planning - Plan Brief

> Full plan: `context/changes/improve-monthly-planning/plan.md`
> Research: `context/changes/improve-monthly-planning/research.md`

## What & Why

This plan implements S-03: faster month preparation with historical expense suggestions, expense-only copying to a chosen month, preserved recurring auto-sync, annual income/savings targets, and alert candidates for future notifications. The user specifically wants suggestions to appear when opening a monthly plan that does not yet exist, based on the same month in the previous year, with confirmation and optional price edits before creation.

## Starting Point

PlanPage already has selected-expense copy mode, but it only copies to the next month. Missing months are currently created by `GetMonthAsync`, which auto-syncs active recurring expenses before any suggestion UI can appear. Statistics has annual actuals/rollups but no persisted `Plan roczny`.

## Desired End State

Opening a missing month first shows selectable historical suggestions from the same month last year. Applying suggestions creates the month, keeps recurring auto-sync, and inserts only selected expense rows with edited planned amounts and zero actuals. Statistics lets the user save year-level expected income and savings targets, and displays deviation alert candidates as preparation only.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Recurring behavior | Preserve auto-sync | Avoids changing existing idempotent month-creation behavior. | User |
| Suggestion timing | Before missing month creation | Matches the requested first-open experience. | User |
| Suggestion source | Same month previous year | Directly supports seasonal/yearly planning. | User + Research |
| Confirmation model | Select and edit before apply | Builds trust and avoids silent historical generation. | User |
| Copy scope | Expenses only | Fits existing copy path and avoids income/savings date ambiguity. | User |
| Annual plan | Persist year-level totals | Makes `Plan roczny` durable without overbuilding monthly grids. | User |
| Alerts | No exclusions yet | Ships a simple +20% candidate foundation. | User |
| Verification | Targeted browser smoke | Catches real Blazor interaction issues without broad e2e scope. | User + Test Plan |

## Scope

**In scope:**

- Preview and apply same-month-previous-year expense suggestions for missing months.
- Scale-based suggested amount rounding: +10%, round up to 10 under 500 and 100 at 500+.
- Preserve recurring auto-sync and suppress obvious duplicates.
- Copy selected expenses to a chosen target month.
- Persist year-level annual income and savings targets.
- Prepare +20% category deviation alert candidates.
- Add service tests, UI contract tests, manual smoke evidence, and cookbook guidance.

**Out of scope:**

- Approval-only recurring sync.
- Copying incomes, savings transfers, or line items.
- Monthly annual-target grids or category annual budgets.
- Category alert exclusions.
- Real notifications.
- Separate `Safe-to-spend`.
- Full browser/e2e automation.

## Architecture / Approach

The backend gets explicit preview/apply contracts so PlanPage can check missing-month suggestions without creating the month. Applying suggestions creates the month through the existing path, so active recurring rows still sync first, then selected historical rows are inserted with duplicate protection. Annual targets are persisted separately from yearly actuals, while alert candidates are added as read-only Statistics data.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Backend preparation and copy contracts | Suggestion preview/apply plus explicit-target expense copy. | Accidentally creating the month during preview. |
| 2. PlanPage first-open UX | Missing-month suggestions and target-month copy controls. | Confusing recurring auto-sync vs suggested rows. |
| 3. Annual targets | Persisted `Plan roczny` totals in Statistics. | Mixing targets with actual monthly finance. |
| 4. Alert candidates | +20% category deviation candidates, no notifications. | Ambiguous historical-average semantics. |
| 5. Verification and evidence | Test/cookbook updates plus browser smoke evidence. | Missing an interaction regression that static tests cannot see. |

**Prerequisites:** S-02 monthly loop is complete; `research.md` exists.
**Estimated effort:** ~4-6 focused sessions across 5 phases.

## Open Risks & Assumptions

- Preserving recurring auto-sync means historical suggestion duplicate detection must be conservative.
- Planned-only future months should not be forced into existing annual actuals tables.
- The first annual plan model intentionally stores only year-level totals.
- Alert candidate math uses the simplest prior-populated-month average unless implementation reveals a better existing pattern.

## Success Criteria (Summary)

- Missing-month PlanPage opens with editable previous-year suggestions before the month is created.
- Applying suggestions preserves recurring auto-sync, creates selected expense rows with edited planned amounts, and copies no actuals or line items.
- Statistics persists annual income/savings targets and displays alert candidates without notification behavior.
