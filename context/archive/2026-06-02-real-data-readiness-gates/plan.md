# Real Data Readiness Gates Implementation Plan

## Overview

Add a focused Phase 2 test rollout that prevents the app, admin panel, readiness evidence, or deployment guidance from presenting real household data as safe before the required manual/external gates are complete. This plan protects the existing readiness architecture rather than adding a new production approval feature.

## Current State Analysis

The app already has the main readiness surfaces from `secure-real-data-readiness`: a database-aware `/health/ready` endpoint, runtime safety options, disabled public `/files` by default, cookie/log/admin checks, an admin readiness panel, and a dedicated evidence file. The remaining risk is false approval. `RealDataReadinessReport.IsAppCheckReady` intentionally means only that automated app checks pass; it does not mean the real-data MVP pilot is fully approved.

`context/changes/secure-real-data-readiness/readiness-evidence.md` still records pending external/manual gates: restore smoke test, live deployed `/health/ready`, Render Blueprint validation, admin panel visual review, and final real-data sign-off. `context/deployment/deploy-plan.md` already states the core operational rule that app rollback does not roll back PostgreSQL schema/data and that meaningful migrations require `pg_dump`, restore smoke evidence, migration review, and rollback/forward-fix notes.

## Desired End State

The test suite contains a cheap but meaningful real-data readiness contract. It proves app-check readiness remains separate from manual evidence, the admin UI does not imply final approval, evidence cannot read as finally approved while required gates are still pending, and deployment guidance keeps the PostgreSQL rollback boundary explicit.

The rollout cookbook records this pattern so future agents do not replace external evidence with fake automated confidence. Live Render health, `pg_dump`, restore smoke, and admin visual review remain manual evidence gates unless the workspace actually has the required external access and tools.

### Key Discoveries:

- `RealDataReadinessService` returns automated checks and manual evidence items separately (`src/HouseholdBudgetMate.Web/Setup/RealDataReadinessService.cs:22`).
- Manual readiness items already include Free Render risk, manual `pg_dump`, restore smoke test, and migration review (`src/HouseholdBudgetMate.Web/Setup/RealDataReadinessService.cs:72`).
- `IsAppCheckReady` derives only from automated checks (`src/HouseholdBudgetMate.Web/Setup/RealDataReadinessService.cs:94`).
- The admin panel labels the state as `App checks ready` and shows separate automated and manual sections (`src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor:252`).
- The evidence file marks live `/health/ready`, Render Blueprint validation, admin review, restore smoke, and final sign-off as pending (`context/changes/secure-real-data-readiness/readiness-evidence.md`).
- `render.yaml` sets Render health checks to `/health/ready` (`render.yaml:9`).
- The deployment plan states that app rollback does not roll back PostgreSQL schema or data (`context/deployment/deploy-plan.md:183`).

## Planning Decisions

| Decision | Choice | Rationale |
| --- | --- | --- |
| Evidence contract | Targeted semantic checks | Stronger than broad source-text checks without building a generic Markdown parser for one artifact. |
| Approval model | Hybrid note only | No app-level approval flag, but the admin/UI contract should stay explicit about where final approval lives. |
| Admin UI testing | Static UI contract plus service tests | Cheapest stable signal for wording and separation; avoids broad MudBlazor rendered setup. |
| Live health check | Manual evidence gate only | Live deployed health depends on external URL/database access and should not be faked in local tests. |
| Deploy rollback policy | Static deployment contract tests | Protects risk #5 without relying on unavailable `pg_dump`, `pg_restore`, or Render CLI tooling. |
| Cookbook scope | Add compact cookbook entry | Keeps the test rollout durable for future agents. |

## What We're NOT Doing

- Not adding a production "final real-data approval" database flag or workflow.
- Not trying to automate live Render checks, `pg_dump`, `pg_restore`, or Render CLI validation in normal tests.
- Not rendering the full MudBlazor admin page with bUnit in this phase.
- Not changing `/health/ready` behavior unless a test exposes an actual regression.
- Not changing Render plans or creating deployment infrastructure.
- Not marking real household data entry as approved while the evidence file still has pending gates.

## Implementation Approach

Add one focused setup/policy test file for the real-data readiness gates and extend the existing readiness service tests where the contract belongs. Keep assertions semantic and risk-based: prove required evidence states are still pending, required operational words are present, and forbidden final-approval implications are absent.

Use static source contract tests for the admin UI and deployment documents because the risk is wording/semantics, not DOM interaction. Use existing `ReadinessHealthTests` as the endpoint behavior coverage and treat live `/health/ready` as a manual evidence item.

## Phase 1: Evidence And Deployment Policy Contracts

### Overview

Add policy tests that keep the external evidence and deployment guidance from drifting into false real-data approval.

### Changes Required:

#### 1. Readiness Evidence Contract Tests

**File**: `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs`

**Intent**: Prove the current evidence record cannot be interpreted as final real-data approval while required external gates remain pending.

**Contract**: Add targeted semantic assertions against `context/changes/secure-real-data-readiness/readiness-evidence.md`. The tests should require evidence coverage for restore smoke, live deployed `/health/ready`, Render Blueprint validation, admin readiness panel review, and final sign-off. Pending required gates must keep the evidence in a not-finally-approved state.

#### 2. Deployment Rollback Boundary Tests

**File**: `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs`

**Intent**: Protect the rollout risk that an app rollback is mistaken for a database rollback.

**Contract**: Assert `context/deployment/deploy-plan.md` still names `pg_dump`, restore smoke-test notes, migration review, rollback or forward-fix notes, and the statement that app rollback does not roll back PostgreSQL schema/data.

#### 3. Render Blueprint Readiness Contract Test

**File**: `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs`

**Intent**: Keep Render wired to the application readiness endpoint without invoking Render tooling.

**Contract**: Assert `render.yaml` contains `healthCheckPath: /health/ready`, keeps `DATABASE_URL` sourced from the managed database, and keeps production public file serving/detailed-error flags in their readiness-safe values.

### Success Criteria:

#### Automated Verification:

- Real-data gate policy tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RealDataReadinessGateTests"`
- Existing readiness endpoint tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ReadinessHealthTests"`

#### Manual Verification:

- Review the evidence assertions and confirm they fail closed when restore smoke, live health, Render validation, admin review, or final sign-off are missing.
- Confirm the tests do not claim to perform real `pg_dump`, restore, or live Render validation.

**Implementation Note**: If implementation discovers the evidence wording changed from pending to complete, stop and verify the external evidence before updating expected assertions.

---

## Phase 2: App Check Vs Final Approval Contract

### Overview

Strengthen the app/admin readiness semantics so automated app checks cannot be confused with full real-data approval.

### Changes Required:

#### 1. Service-Level Separation Test

**File**: `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessServiceTests.cs`

**Intent**: Make the meaning of `IsAppCheckReady` explicit: app checks can pass while manual evidence still remains outside automated approval.

**Contract**: Add or strengthen an assertion that a passing report still contains manual items for `Manual pg_dump`, restore smoke test, and migration review, and that `IsAppCheckReady` is not treated as a final approval signal.

#### 2. Admin UI Source Contract Test

**File**: `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs`

**Intent**: Keep the admin panel wording from implying that automated checks equal final real-data approval.

**Contract**: Add static source assertions against `src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor`. Require the `App checks ready` wording, separate automated/manual evidence sections, the evidence path display, and copy that names Free Render as accepted pilot risk. Forbid final-approval wording such as `durable production`, `real data approved`, or equivalent "all clear" language unless it is clearly negated or scoped as pending/manual evidence.

#### 3. Hybrid Note Scope

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor`

**Intent**: Use only a minimal UI wording adjustment if tests show the existing admin text does not clearly point final approval to manual evidence.

**Contract**: Keep this optional and scoped. Do not add storage, a new approval flag, or a new workflow. If wording changes are needed, they must clarify that real-data decision requires manual `pg_dump`, restore smoke, and migration review evidence.

### Success Criteria:

#### Automated Verification:

- Readiness service tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RealDataReadinessServiceTests"`
- Real-data gate UI contract tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RealDataReadinessGateTests"`

#### Manual Verification:

- Review `AdminConfig.razor` and confirm the page reads as "app checks plus manual evidence", not as a final real-data approval feature.
- Confirm no production approval flag, database field, route, or workflow was added.

**Implementation Note**: If the current admin copy already satisfies the contract, this phase should add tests without changing production UI.

---

## Phase 3: Cookbook And Rollout Closure

### Overview

Record the real-data readiness gate pattern in the central test-plan cookbook and run the broader verification gates.

### Changes Required:

#### 1. Rollout Cookbook Entry

**File**: `context/foundation/test-plan.md`

**Intent**: Document how future agents should test real-data readiness without pretending external operational proof is automatable.

**Contract**: Fill `6.2 Adding a real-data readiness gate test` with reference tests, decision rules, and commands. Include the split between app-check tests, evidence/deploy policy tests, static admin UI contract tests, and manual smoke evidence for live `/health/ready`, `pg_dump`, restore, Render validation, and admin visual review.

#### 2. Phase Notes

**File**: `context/foundation/test-plan.md`

**Intent**: Mark the Phase 2 shipped pattern in the rollout notes.

**Contract**: Add a concise `6.5` note that Phase 2 shipped policy/setup tests plus manual evidence gates for risks #2 and #5, with no browser/e2e or live Render automation.

#### 3. Final Verification

**File**: `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs`

**Intent**: Ensure the new gates coexist with existing setup, readiness, and runtime safety tests.

**Contract**: Run targeted setup tests, then the full release test suite and build. Keep the new tests under the existing xUnit/FluentAssertions style.

### Success Criteria:

#### Automated Verification:

- Setup/readiness tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RealDataReadiness|FullyQualifiedName~ReadinessHealth|FullyQualifiedName~RuntimeSafety|FullyQualifiedName~SessionCookie"`
- Full release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`
- Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- Git whitespace check passes: `git diff --check -- .`

#### Manual Verification:

- Review `context/foundation/test-plan.md 6.2` and confirm it explains app checks vs manual evidence.
- Confirm the final evidence/deployment tests protect risks #2 and #5 without claiming live external validation.
- Confirm any later commit can exclude Markdown files if requested, while local rollout artifacts remain updated.

**Implementation Note**: Do not mark real-data readiness as complete for actual household data entry unless the evidence file itself records completed or explicitly accepted manual evidence.

---

## Testing Strategy

### Unit Tests:

- No pure domain unit tests are expected.
- Existing runtime safety and session cookie tests remain supporting coverage.

### Integration / Setup Tests:

- Add `RealDataReadinessGateTests` as policy/setup contract coverage.
- Extend `RealDataReadinessServiceTests` only where service-level semantics belong.
- Reuse existing `ReadinessHealthTests` for `/health/ready` behavior; do not duplicate endpoint internals.

### Static Contract Tests:

- Read `readiness-evidence.md`, `deploy-plan.md`, `render.yaml`, and `AdminConfig.razor`.
- Prefer targeted semantic assertions over broad snapshots or brittle whole-file comparisons.
- Assert forbidden false-approval wording narrowly enough that negated/pending copy is allowed.

### Manual Testing Steps:

1. Review the evidence file and confirm restore smoke, live health, Render validation, admin review, and final sign-off are not accidentally marked complete.
2. Review the admin readiness section and confirm it distinguishes automatic checks from manual evidence.
3. Review deployment guidance and confirm app rollback vs PostgreSQL rollback is explicit.
4. If live Render access exists outside this task, record `/health/ready`, `pg_dump`, restore smoke, and admin panel review results in the existing evidence file.

## Performance Considerations

The new tests should read a handful of local files and run existing setup services. Runtime impact should be minimal. Avoid introducing live network calls, process execution for database tools, or rendered UI setup that would make the readiness gate slow or flaky.

## Migration Notes

No production schema, data, or deployment migration is planned. The rollback risk is protected through documentation/evidence contract tests, not by changing EF migration behavior.

## References

- Related research: `context/changes/real-data-readiness-gates/research.md`
- Rollout plan: `context/foundation/test-plan.md`
- Current evidence: `context/changes/secure-real-data-readiness/readiness-evidence.md`
- Deployment guidance: `context/deployment/deploy-plan.md`
- Existing readiness service: `src/HouseholdBudgetMate.Web/Setup/RealDataReadinessService.cs:22`
- Existing admin panel: `src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor:252`
- Existing readiness endpoint: `src/HouseholdBudgetMate.Web/Setup/ReadinessEndpoint.cs:8`
- Existing readiness tests: `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessServiceTests.cs:14`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` - <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Evidence And Deployment Policy Contracts

#### Automated

- [x] 1.1 Real-data gate policy tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RealDataReadinessGateTests"` — a55cfab
- [x] 1.2 Existing readiness endpoint tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ReadinessHealthTests"` — a55cfab

#### Manual

- [x] 1.3 Review the evidence assertions and confirm they fail closed when required manual/live gates are missing — a55cfab
- [x] 1.4 Confirm the tests do not claim to perform real `pg_dump`, restore, or live Render validation — a55cfab

### Phase 2: App Check Vs Final Approval Contract

#### Automated

- [x] 2.1 Readiness service tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RealDataReadinessServiceTests"` — 8fb67dc
- [x] 2.2 Real-data gate UI contract tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RealDataReadinessGateTests"` — 8fb67dc

#### Manual

- [x] 2.3 Review `AdminConfig.razor` and confirm it reads as "app checks plus manual evidence" — 8fb67dc
- [x] 2.4 Confirm no production approval flag, database field, route, or workflow was added — 8fb67dc

### Phase 3: Cookbook And Rollout Closure

#### Automated

- [x] 3.1 Setup/readiness tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RealDataReadiness|FullyQualifiedName~ReadinessHealth|FullyQualifiedName~RuntimeSafety|FullyQualifiedName~SessionCookie"`
- [x] 3.2 Full release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`
- [x] 3.3 Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- [x] 3.4 Git whitespace check passes: `git diff --check -- .`

#### Manual

- [x] 3.5 Review `context/foundation/test-plan.md 6.2` and confirm it explains app checks vs manual evidence
- [x] 3.6 Confirm the final evidence/deployment tests protect risks #2 and #5 without claiming live external validation
- [x] 3.7 Confirm any later commit can exclude Markdown files if requested, while local rollout artifacts remain updated
