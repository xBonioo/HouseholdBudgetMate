# Quality Cookbook And Gates Implementation Plan

## Goal

Close test-rollout Phase 4 by turning the shipped testing patterns into durable cookbook guidance and adding a deterministic local contract test for `context/foundation/test-plan.md`.

The practical scope is deliberately narrow: deterministic tests and cookbook hygiene only. Browser/AI-native review is out of implementation scope for this change.

## Current State Analysis

`context/foundation/test-plan.md` already contains the rollout strategy, risk map, gate table, and shipped recipes for:

- cross-screen monthly consistency;
- real-data readiness gates;
- access restore and ownership boundaries.

The remaining gap is `section 6.4 Adding a quality gate or selective AI-native review`, which is still `TBD`. The document also currently has a broken Phase 3 note where `recovery-boundary-test` is split by a control/newline artifact.

Existing tests already establish the repo-file contract pattern:

- `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs` reads repository docs and asserts policy/setup contracts.
- `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs` reads Razor/source files and asserts UI contract text.

Important repository constraint: `context/` is ignored and not tracked by git. The new test can protect the local quality workflow, but it is not a portable CI gate unless the project later chooses to track the relevant context artifacts or package them for CI.

## Decisions

| Decision | Choice | Rationale |
| --- | --- | --- |
| Commit scope | Code-only commit; markdown remains local | Matches the user's repeated preference not to commit `.md` files. |
| Test shape | New static `TestPlanQualityGateTests` | Cheapest deterministic guard for the Phase 4 artifact. |
| AI-native/browser | Out of practical implementation scope | User selected deterministic-only scope; avoid creating an optional/manual review gate here. |
| Gate portability | Treat as local quality gate | `context/` is ignored by git, so claiming CI portability would be false. |

## Scope

### In Scope

- Add a static test file under `src/HouseholdBudgetMate.Tests/Tests/Setup/`.
- Repair local `context/foundation/test-plan.md` Phase 3 note formatting.
- Replace `section 6.4` `TBD` with deterministic quality-gate cookbook guidance.
- Add a Phase 4 rollout note in `section 6.5`.
- Keep implementation and commit workflow compatible with no-md commits.

### Out Of Scope

- Browser/e2e automation.
- AI-native review workflow or prompt design.
- CI changes to include `context/`.
- New app product behavior.
- Reworking previous Phase 1-3 tests.

## Phase 1: Test-Plan Contract And Cookbook Closure

### Changes Required

#### `src/HouseholdBudgetMate.Tests/Tests/Setup/TestPlanQualityGateTests.cs`

**Intent**: Add a local deterministic contract test for the rollout plan so future changes cannot silently remove shipped gates, cookbook recipes, or negative-space rules.

**Contract**:

- Namespace should follow the existing setup tests: `HouseholdBudgetMate.Tests.Tests.Setup`.
- Use xUnit and FluentAssertions.
- Reuse the existing `ReadRepoFile` helper pattern from `RealDataReadinessGateTests`.
- Read `context/foundation/test-plan.md`.
- Normalize line endings before assertions.
- Assert the document contains shipped gate names:
  - `build/typecheck`;
  - `unit + integration tests`;
  - `targeted monthly-loop contract`;
  - `real-data readiness contract`;
  - `access restore/security regression tests`;
  - `e2e/browser critical flow`.
- Assert cookbook recipes contain their reference tests:
  - `MonthlyBudgetingLoopTests.cs`;
  - `MonthlyBudgetingLoopUiTests.cs`;
  - `MonthlyBudgetingLoopRenderedTests.cs`;
  - `RealDataReadinessGateTests.cs`;
  - `RealDataReadinessServiceTests.cs`;
  - `ReadinessHealthTests.cs`;
  - `RecoveryBoundaryTests.cs`;
  - `AccessHardeningRedirectMiddlewareTests.cs`.
- Assert `section 6.4` is no longer `TBD`.
- Assert `section 6.4` preserves deterministic-first guidance:
  - promote a gate only after a shipped risk pattern exists;
  - document owner/location, requiredness, command or manual evidence, and regression caught;
  - keep manual evidence explicit for external systems;
  - use browser/e2e only when deterministic layers cannot observe the risk;
  - do not use AI-native review as a replacement for deterministic tests.
- Assert rollout notes are parseable:
  - Phase 1 contains `testing-cross-screen-monthly-consistency`;
  - Phase 2 contains `real-data-readiness-gates`;
  - Phase 3 contains `recovery-boundary-test`;
  - Phase 4 contains `quality-cookbook-and-gates`;
  - no note line contains broken `(` followed by a newline before the change id.
- Assert exclusions preserve:
  - OCR/file upload paths out of scope;
  - no full-page visual snapshots everywhere;
  - no coverage padding on generated or shape-only contracts.

#### `context/foundation/test-plan.md`

**Intent**: Close the Phase 4 cookbook gap locally so the new contract test has a real oracle and future agents can follow the rollout rules.

**Contract**:

- Fix the broken Phase 3 note so `recovery-boundary-test` appears on the same line as `Phase 3 (...)`.
- Replace `section 6.4` `TBD` with a short deterministic cookbook recipe.
- Remove practical AI-native review instructions from implementation scope. If AI-native is mentioned, it must be framed as "do not use as a replacement for deterministic tests"; no operational browser/AI workflow should be added.
- Add a Phase 4 note under `section 6.5` naming `quality-cookbook-and-gates` and stating that the shipped protection is a local static contract test plus cookbook cleanup.
- Preserve the existing cost x signal strategy and the Phase 1-3 recipe guidance.

### Automated Verification

- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~TestPlanQualityGateTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RealDataReadinessGateTests|FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~TestPlanQualityGateTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`
- `git diff --check -- .`

### Manual Verification

- Confirm `context/foundation/test-plan.md` no longer has the broken Phase 3 line.
- Confirm `section 6.4` reads as deterministic quality-gate guidance, not a browser/AI workflow.
- Confirm git staging/commit excludes `.md` files.

### Implementation Note

Because `context/` is ignored, the markdown edits are local workflow state. The code test can be committed by itself if the user requests a commit without markdown, but that commit depends on the local context file to pass in this workspace.

---

## Phase 2: Plan Progress And Handoff Hygiene

### Changes Required

#### `context/changes/quality-cookbook-and-gates/plan.md`

**Intent**: Record implementation progress mechanically after Phase 1 lands so future `/10x-implement` runs can resume from the correct state.

**Contract**:

- Mark all Phase 1 automated/manual items complete only after verification passes.
- Append the commit SHA only to code-committed items if the user commits code without markdown.
- Leave markdown-only progress local unless the user explicitly allows committing markdown.

#### `context/changes/quality-cookbook-and-gates/change.md`

**Intent**: Move the change status to implemented after Phase 1 succeeds and the user confirms no further Phase 4 scope.

**Contract**:

- Set `status: implemented`.
- Update `updated: 2026-06-02`.

### Automated Verification

- `Test-Path context/changes/quality-cookbook-and-gates/plan.md`
- `Test-Path context/changes/quality-cookbook-and-gates/plan-brief.md`
- `rg -n "quality-cookbook-and-gates|TestPlanQualityGateTests|status: implemented" context/changes/quality-cookbook-and-gates context/foundation/test-plan.md`

### Manual Verification

- Confirm no additional rollout phase remains for this change.
- Confirm any final commit still excludes `.md` files unless the user explicitly changes the rule.

---

## Testing Strategy

### Unit / Contract Tests

- New `TestPlanQualityGateTests` is the primary test.
- It should remain semantic: assert required phrases and section-level invariants, not exact full-document text.

### Integration Tests

- No new app integration tests are needed. Phase 4 is not changing product behavior.

### Manual Tests

- Review the cookbook text for clarity and no browser/AI operational scope.
- Review git staging before commit.

## Risks And Mitigations

| Risk | Mitigation |
| --- | --- |
| Test overfits markdown wording | Assert semantic anchors and required references, not complete paragraphs. |
| Code-only commit leaves CI unable to reproduce local context test | State this explicitly; treat the gate as local while `context/` remains ignored. |
| Phase 4 drifts into browser/AI tooling | Keep browser/AI operational work out of scope and assert deterministic-first guidance. |
| Existing mojibake/control artifacts remain in `test-plan.md` | Add parseability assertions for rollout notes. |

## References

- Research: `context/changes/quality-cookbook-and-gates/research.md`
- Test plan: `context/foundation/test-plan.md`
- Existing repository-file contract pattern: `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs`
- Existing static UI contract pattern: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append `-- <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Test-Plan Contract And Cookbook Closure

#### Automated

- [x] 1.1 `TestPlanQualityGateTests` passes -- ab87b34
- [x] 1.2 Related repository-file contract tests pass -- ab87b34
- [x] 1.3 Full release test suite passes -- ab87b34
- [x] 1.4 Diff whitespace check passes -- ab87b34

#### Manual

- [x] 1.5 Phase 3 rollout note is repaired locally -- ab87b34
- [x] 1.6 Section 6.4 is deterministic cookbook guidance -- ab87b34
- [x] 1.7 Git staging excludes markdown files -- ab87b34

### Phase 2: Plan Progress And Handoff Hygiene

#### Automated

- [x] 2.1 Plan and brief files exist
- [x] 2.2 Handoff markers are searchable

#### Manual

- [x] 2.3 No additional rollout phase remains
- [x] 2.4 Final commit rule is confirmed
