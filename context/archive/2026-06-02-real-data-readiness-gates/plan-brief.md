# Real Data Readiness Gates - Plan Brief

> Full plan: `context/changes/real-data-readiness-gates/plan.md`
> Research: `context/changes/real-data-readiness-gates/research.md`

## What & Why

This plan adds Phase 2 rollout protection for real-data readiness gates. The risk is false approval: automated app checks might be mistaken for final permission to enter real household data before backup, restore, live health, Render, admin review, and migration evidence are complete.

## Starting Point

The readiness layer already exists: `/health/ready`, runtime safety checks, admin readiness UI, Render health config, and `readiness-evidence.md`. The evidence file still has pending manual/external gates, and the deployment plan already warns that app rollback does not roll back PostgreSQL.

## Desired End State

Tests keep app-check readiness separate from manual evidence and prevent docs/UI from implying final approval too early. Deployment guidance remains explicit about `pg_dump`, restore smoke, migration review, and PostgreSQL rollback boundaries. The cookbook records the pattern for future rollout phases.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Evidence contract | Targeted semantic checks | Stronger than raw text checks without building a Markdown parser for one artifact. | Plan |
| Approval model | Hybrid note only | Clarifies manual evidence in UI without adding a new production approval feature. | Plan |
| Admin UI testing | Static source contract plus service tests | Cheapest stable signal for wording and separation. | Plan |
| Live health | Manual evidence gate only | Live Render checks depend on external URL/database access. | Research / Plan |
| Rollback policy | Static deploy contract tests | Protects DB rollback risk without requiring unavailable tools. | Research / Plan |
| Cookbook | Update `test-plan.md 6.2` | Keeps the shipped pattern reusable by future agents. | Plan |

## Scope

**In scope:**

- New real-data readiness policy/setup tests.
- Stronger `IsAppCheckReady` vs manual evidence assertions.
- Static admin UI contract checks.
- Static evidence/deployment/render config contract checks.
- Cookbook update for real-data readiness gates.

**Out of scope:**

- New production final-approval flag or workflow.
- Automated live Render checks.
- Automated `pg_dump`, `pg_restore`, or Render CLI validation.
- Full rendered MudBlazor admin-page tests.
- Database schema or deployment infrastructure changes.

## Architecture / Approach

The plan treats readiness as two layers. App-checkable state stays in code and tests; manual operational evidence stays in `readiness-evidence.md` and human review. Tests guard the boundary between those layers by checking service semantics, admin wording, evidence status, deployment policy, and Render config.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Evidence And Deployment Policy Contracts | New policy tests for evidence, deploy plan, and Render config | Evidence/docs imply readiness while gates are pending |
| 2. App Check Vs Final Approval Contract | Stronger service/admin UI contract | `IsAppCheckReady` is treated as final real-data approval |
| 3. Cookbook And Rollout Closure | Cookbook entry and full verification | Pattern ships but future agents do not know how to reuse it |

**Prerequisites:** Existing `secure-real-data-readiness` readiness layer and evidence file remain available.
**Estimated effort:** About 2-3 focused implementation sessions across 3 phases.

## Open Risks & Assumptions

- Evidence wording may change; tests should stay semantic enough to avoid brittle whole-file snapshots.
- If manual evidence has genuinely been completed outside this workspace, Phase 1 should pause and update the contract with the actual evidence state.
- Local tests cannot prove live Render health or database restore without external access and tools.

## Success Criteria (Summary)

- App checks can pass without being presented as final real-data approval.
- Evidence/deployment tests fail closed when restore, live health, Render, admin review, or final sign-off gates are missing.
- Cookbook explains the app-check vs manual-evidence pattern and keeps risks #2 and #5 covered.
