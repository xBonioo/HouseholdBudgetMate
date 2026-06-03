# Quality Cookbook And Gates - Plan Brief

> Full plan: `context/changes/quality-cookbook-and-gates/plan.md`
> Research: `context/changes/quality-cookbook-and-gates/research.md`

## What & Why

This plan closes Phase 4 of the test rollout by turning shipped test patterns into durable cookbook guidance and adding a local deterministic contract test for `context/foundation/test-plan.md`.

The goal is to prevent future agents from drifting back into broad e2e, AI-native review, mirror tests, or broken rollout notes when the project already has cheaper high-signal test layers.

## Starting Point

The test plan already has recipes for monthly consistency, real-data readiness, and recovery boundaries. The remaining gap is `section 6.4`, which is still `TBD`, and there is a broken Phase 3 rollout note for `recovery-boundary-test`.

`context/` is ignored by git, so markdown updates are local workflow state unless the user explicitly allows committing them.

## Desired End State

`test-plan.md` has a complete deterministic quality-gate cookbook section, parseable rollout notes, and no Phase 4 `TBD`. A new `TestPlanQualityGateTests` file protects the local rollout artifact from losing shipped gates, recipe references, and negative-space rules.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Commit scope | Code-only commit; markdown local | Matches the user's repeated no-md commit rule. | User |
| Test shape | Static `TestPlanQualityGateTests` | Cheapest deterministic signal for a policy/cookbook artifact. | Research/User |
| AI-native/browser | Out of practical scope | User selected deterministic-only scope. | User |
| Portability | Local gate while `context/` is ignored | The test depends on local context artifacts. | Plan |

## Scope

**In scope:**

- Add `src/HouseholdBudgetMate.Tests/Tests/Setup/TestPlanQualityGateTests.cs`.
- Repair local `context/foundation/test-plan.md`.
- Replace `section 6.4` `TBD` with deterministic gate cookbook guidance.
- Add a Phase 4 rollout note.

**Out of scope:**

- Browser/e2e automation.
- AI-native review workflow.
- CI changes to track or package `context/`.
- Product behavior changes.

## Approach

Use the existing repository-file contract-test style from `RealDataReadinessGateTests`: read `context/foundation/test-plan.md`, normalize line endings, and assert semantic anchors for gates, cookbook references, rollout-note parseability, and exclusions. Keep markdown edits local and stage only code if a commit is requested.

## Phases At A Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Test-plan contract and cookbook closure | Adds the static test and repairs/completes the local cookbook. | Test overfits wording or markdown remains broken. |
| 2. Plan progress and handoff hygiene | Marks progress and final handoff state. | Accidentally staging markdown despite the no-md rule. |

**Prerequisites:** Existing `context/foundation/test-plan.md` remains present in the local workspace.

**Estimated effort:** One implementation pass plus verification.

## Open Risks & Assumptions

- The code-only commit will not include the markdown oracle it tests because `context/` is ignored.
- If the project later wants this gate in CI, it must decide how to track or package the relevant context files.

## Success Criteria

- `TestPlanQualityGateTests` passes locally.
- Full release test suite passes.
- `test-plan.md` has no Phase 4 `TBD` and no broken Phase 3 rollout note.
- Any commit excludes `.md` files unless explicitly approved.
