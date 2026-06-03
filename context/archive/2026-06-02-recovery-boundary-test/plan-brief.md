# Recovery Boundary Test - Plan Brief

> Full plan: `context/changes/recovery-boundary-test/plan.md`
> Research: `context/changes/recovery-boundary-test/research.md`

## What & Why

This plan adds Phase 3 rollout protection for access restore boundaries. The risk is that local recovery resets an administrator but an old trusted-browser cookie or wrong ownership scope still exposes shared household data.

## Starting Point

The app already separates visible PIN-protected profiles from the technical `default-user` budget owner. Existing tests cover session restore, recovery, and budget scoping separately, but no test currently proves the cross-component behavior after recovery changes persisted credentials.

## Desired End State

Tests prove an old trusted cookie minted before recovery fails after recovery resets the admin PIN. The recovered visible admin can sign in with the new PIN, gets `BudgetOwnerUserId = default-user`, and can see only the intended shared budget scope. Recovery routing also prefers `/access-recovery` over `/access-setup`.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Boundary coverage | Several focused cross-component tests | Easier failure diagnosis than one long scenario while still testing real integration. | User / Research |
| Middleware | Add recovery-priority test | Pins the route ordering risk cheaply without UI/browser tests. | User / Research |
| Budget fixture | Seed `default-user` owned row | Proves the data-scope risk, not just session state. | User / Research |
| Phasing | Two phases | Separates service/security tests from routing and cookbook closure. | User / Plan |
| Production changes | Test-only by default | The feature exists; this rollout phase protects behavior unless a bug appears. | Plan |

## Scope

**In scope:**

- New recovery/session/scope integration-security tests.
- Real `UserService`, `UserSessionService`, `AccessRecoveryService`, persistence, Data Protection, and fake cookie JS runtime.
- `default-user` owned budget row assertions.
- Middleware recovery-priority and remote-denial tests.
- Cookbook update in `context/foundation/test-plan.md`.

**Out of scope:**

- New recovery UI or workflow.
- Removing trusted-browser restore for valid current profiles.
- Migrating financial rows away from `default-user`.
- Browser/e2e tests or live infrastructure checks.
- Making `default-user` interactive.

## Architecture / Approach

The plan tests the real boundary instead of isolated mocks: recovery mutates persisted admin credentials, session restore checks the current persisted security stamp, and EF query filters enforce the active budget-owner scope. Middleware coverage stays separate and route-level.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Recovery Session And Scope Boundary | Split integration tests for stale cookie, recovered admin scope, and technical-owner exclusion | Old session survives recovery or reads wrong budget scope |
| 2. Recovery Routing And Cookbook Closure | Recovery-priority middleware tests plus rollout cookbook update | Recovery path is routed incorrectly or pattern is not reusable |

**Prerequisites:** Existing access hardening, recovery, session, and scoping services remain available.
**Estimated effort:** About 1-2 focused implementation sessions across 2 phases.

## Open Risks & Assumptions

- If stale trusted restore currently succeeds after recovery, that is a real bug and should be fixed rather than accepted.
- Test helper setup may need careful service construction to keep the test readable without over-abstracting the oracle.
- Markdown/context artifacts may stay uncommitted later if the user asks for code-only commits.

## Success Criteria (Summary)

- Old trusted cookie fails and clears after recovery resets the admin PIN.
- New PIN signs in the visible admin with `BudgetOwnerUserId = default-user`; `default-user` remains non-interactive.
- Recovery-required routing goes to `/access-recovery`, and the cookbook records the pattern.
