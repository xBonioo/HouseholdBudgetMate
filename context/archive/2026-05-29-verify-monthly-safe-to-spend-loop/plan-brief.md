# Verify Monthly Budgeting Loop - Plan Brief

> Full plan: `context/changes/verify-monthly-safe-to-spend-loop/plan.md`
> Research: `context/foundation/roadmap.md`, completed F-02/S-01 plans

## What & Why

S-02 proves the product's north-star loop: a PIN-unlocked household member can manage a month, record planned/real/unexpected expenses, see reliable `Live balance` and plan progress, and complete `close -> reopen -> edit -> close`.

## Starting Point

The app already has a month plan UI, expense entry/editing, account balances, income, savings transfers, PIN-gated sessions, and real-data readiness work. The separate safe-to-spend value was rejected by the user on 2026-05-29 and reaffirmed as out of scope on 2026-05-30, so the accepted loop validates existing monthly-finance semantics rather than adding a new financial result.

## Desired End State

Plan, Dashboard, and Accounts consistently show accepted live-balance/month-state values and incomplete-balance guidance. A controlled demo scenario proves the values after planned expense entry, real spending, unexpected expense entry, savings transfer timing, and `close -> reopen -> edit -> close`. Component/UI contract automation and `acceptance-evidence.md` provide the current proof; full clicked-flow sign-off remains the final closure item.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Financial result | Do not add separate safe-to-spend | User explicitly rejected it after phase 2 rollback. | User |
| Acceptance data | Controlled demo scenario | Deterministic data makes expected values reviewable and repeatable. | Plan |
| Month lifecycle | Test close, reopen, edit, close | S-02 should prove the loop remains valid across lifecycle transitions. | Plan |
| Readiness gate | Complete F-02 external evidence before real data | The controlled S-02 scenario is demo data; real household data still needs backup/restore/live evidence. | Plan |
| Automation depth | Component/UI contract plus service tests | The repo has no browser harness, so phase 4 uses the smallest maintainable UI contract test. | Implementation |
| Evidence | `acceptance-evidence.md` | S-02 needs a readable final proof with values, commands, and sign-off. | Plan |

## Scope

**In scope:**

- Complete or explicitly approve F-02 readiness evidence before real household data entry.
- Add deterministic service tests for the monthly loop and month lifecycle.
- Verify `Live balance`, `Pozostalo w planie`, savings transfer timing, and incomplete-balance guidance.
- Add component/UI contract automation for the accepted screen semantics.
- Record final evidence in `acceptance-evidence.md` and update roadmap status only after sign-off.

**Out of scope:**

- Adding a separate safe-to-spend value, reserve fields, or UI KPI.
- Redesigning PIN access or real-data readiness.
- Schema migrations unless implementation reveals an unavoidable testability issue.
- OCR, recurring generation, next-month copy automation, public API, or real household data entry without sign-off.

## Architecture / Approach

Use `IncomeService.GetLiveBalanceAsync` as the single live-balance source and existing month KPI outputs as the plan-progress source. UI components render those contracts without local recalculation. Service tests establish deterministic expected values, and a small component/UI contract test proves the accepted user-visible surfaces.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Readiness and baseline gate | F-02 evidence is approved for controlled demo and S-02 evidence file exists | Readiness manual items still block real data |
| 2. Scope baseline | Accepted live-balance/month-state model is confirmed | Old safe-to-spend assumptions can leak back in |
| 3. Service scenario | Deterministic monthly-loop and lifecycle tests | Expected numbers must be easy to audit |
| 4. UI automation | Component/UI contract for accepted Plan/Dashboard/Accounts semantics | Full clicked browser path remains a sign-off item |
| 5. Final evidence | Signed acceptance and roadmap closure | S-02 could be marked done before external evidence is complete |

**Prerequisites:** F-02 readiness evidence must be completed or explicitly approved before real household data entry.
**Estimated effort:** remaining work is focused on external readiness evidence and final clicked-flow sign-off.

## Open Risks & Assumptions

- Full browser-click automation may require new packages and test-host helpers because the repo has no current UI automation harness.
- The controlled scenario proves the product loop without using real household data; real data remains gated by F-02 sign-off.
- The strict readiness prerequisite can block real-data use until backup, restore, live health, and admin-panel evidence are completed.

## Success Criteria (Summary)

- A PIN-unlocked household member can complete the controlled monthly loop and see reliable `Live balance`, plan progress, savings timing, and incomplete-state guidance.
- Service and component/UI contract tests prove formula updates, unexpected expense behavior, close/reopen lifecycle, and accepted screen semantics.
- `acceptance-evidence.md` records expected vs actual values, command results, UI evidence, and final human sign-off.
