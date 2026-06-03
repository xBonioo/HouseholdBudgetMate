# Testing Cross Screen Monthly Consistency — Plan Brief

> Full plan: `context/changes/testing-cross-screen-monthly-consistency/plan.md`
> Research: `context/changes/testing-cross-screen-monthly-consistency/research.md`

## What & Why

This plan ships rollout Phase 1 from `context/foundation/test-plan.md`: protect monthly edits from producing different budget stories across Plan, Dashboard/Home, Accounts, and Statistics. It also guards the accepted no-Safe-to-spend contract and keeps incomplete-balance guidance visible.

## Starting Point

The S-02 monthly loop already has service tests and static UI contract tests. The gap is that the edited scenario does not yet assert Dashboard/Home and Statistics projections in the same oracle-backed flow, and there is no rendered UI smoke layer.

## Desired End State

After this plan lands, one deterministic monthly edit scenario proves the relevant service projections agree after reload. A small rendered smoke test confirms the monthly UI contract can render, while static UI guards keep screen-specific labels and no-Safe-to-spend wording stable.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Test DB | Existing InMemory fixture | Matches current monthly-loop tests and keeps Phase 1 focused. | Plan |
| Projection scope | Four projections | Covers Plan, Home, Accounts, and Statistics screen roles without browser tooling. | Plan |
| UI layer | Small bUnit rendered smoke | Honors rendered UI coverage while avoiding full page/browser scope. | Plan + Context7 |
| Static UI contract | Tighten existing tests | Cheaply guards labels, service wiring, incomplete guidance, and no Safe-to-spend. | Research |
| Cookbook | Update §6 now | Captures the shipped pattern while the implementation is fresh. | Plan |

## Scope

**In scope:**

- Extend `MonthlyBudgetingLoopTests` for cross-screen service projection agreement.
- Add bUnit as a narrow rendered smoke layer.
- Tighten `MonthlyBudgetingLoopUiTests`.
- Update `context/foundation/test-plan.md §6`.

**Out of scope:**

- Browser/e2e or Playwright tests.
- Full rendered tests for every page.
- Production formula, DTO, schema, or UI redesign.
- Reintroducing Safe-to-spend.

## Architecture / Approach

The numeric oracle lives in service integration tests using the accepted scenario from `acceptance-evidence.md`. UI confidence is layered: rendered smoke for minimal Blazor rendering signal, static source contracts for screen roles and labels, and no browser/e2e unless a future risk is specifically about already-open-screen behavior.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Cross-screen projection integration | One edited scenario asserts month, live balance, dashboard, and statistics projections. | Implementation-mirror expected values. |
| 2. Rendered UI smoke contract | bUnit smoke coverage for accepted monthly UI state. | Scope creep into full page rendering. |
| 3. Static UI contract and cookbook | Stronger source guards plus §6 rollout recipe. | Cookbook too vague for future agents. |

**Prerequisites:** Existing S-02 tests and `research.md` are present.
**Estimated effort:** ~2-3 focused implementation sessions across 3 phases.

## Open Risks & Assumptions

- Assumes InMemory is enough for this risk; SQLite remains a future option for relational/provider-specific regressions.
- Assumes a narrow bUnit smoke target is feasible without broad MudBlazor/router/dialog setup.
- Assumes Statistics should remain finance rollup context, not a Live-balance screen.

## Success Criteria (Summary)

- Targeted and full `dotnet test` runs pass.
- Rendered smoke and static UI contract tests guard the accepted monthly contract.
- `context/foundation/test-plan.md §6.1` explains how to add future cross-screen monthly consistency tests.
