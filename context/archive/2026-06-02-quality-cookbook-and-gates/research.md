---
change_id: quality-cookbook-and-gates
topic: quality-cookbook-and-gates
date: 2026-06-02T19:48:09.5873623+02:00
researcher: Codex
git_commit: e26c3466e970c7d69bbe43b5fdde7b4c49a4f866
branch: main
repository: HouseholdBudgetMate
tags: [research, codebase, test-plan, quality-gates, cookbook]
status: complete
last_updated: 2026-06-02
last_updated_by: Codex
---

## Summary

Phase 4 is a meta-quality phase, not a new product feature. The oracle is `context/foundation/test-plan.md`: shipped rollout patterns must become durable cookbook guidance, and required gates must be named so future test work can choose the cheapest reliable signal instead of defaulting to broad UI/e2e or mirror tests.

The main implementation opportunity is a deterministic policy/contract test around `context/foundation/test-plan.md`, plus a focused cookbook update. This should lock that:

- `section 5 Quality Gates` names the shipped required gates for monthly consistency, real-data readiness, and access restore/security.
- `section 6.1`, `section 6.2`, and `section 6.3` keep reference tests, source-of-truth guidance, cheap-layer decisions, and "do not use browser/e2e just because" boundaries.
- `section 6.4` is no longer `TBD` and describes how to add a quality gate or selective AI-native review.
- `section 6.5` per-phase notes remain parseable and do not regress into broken or ambiguous rollout state.
- `section 7 Exclusions` preserves negative-space rules such as no OCR/file upload tests, no broad visual snapshots, and no coverage-padding tests.

## Detailed Findings

### Current Rollout State

`context/foundation/test-plan.md` already lists Phase 4 as `Quality cookbook and gates`, with intent to "turn shipped patterns into cookbook entries and name required gates" (`context/foundation/test-plan.md:49`). It also says browser/e2e is not currently part of the stack and should only be used if research proves cheaper layers insufficient (`context/foundation/test-plan.md:63`). AI-native review is available only as a selective local/manual layer, not a substitute for deterministic assertions (`context/foundation/test-plan.md:64`, `context/foundation/test-plan.md:69`).

The quality gate table has already accumulated the shipped gates:

- build/typecheck and unit/integration remain required (`context/foundation/test-plan.md:78`, `context/foundation/test-plan.md:79`).
- monthly-loop contract is required after Phase 1 (`context/foundation/test-plan.md:80`).
- real-data readiness contract is required after Phase 2 (`context/foundation/test-plan.md:81`).
- access restore/security regression tests are required after Phase 3 (`context/foundation/test-plan.md:82`).
- e2e/browser remains conditional, and selective AI-native review remains optional after Phase 4 (`context/foundation/test-plan.md:83`, `context/foundation/test-plan.md:84`).

`section 6.4 Adding a quality gate or selective AI-native review` is still `TBD` (`context/foundation/test-plan.md:156`, `context/foundation/test-plan.md:158`). That is the primary Phase 4 gap.

### Existing Cookbook Patterns

Phase 1 cookbook (`section 6.1`) has clear rules for monthly consistency:

- primary numeric guard is `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs` (`context/foundation/test-plan.md:96`).
- static UI contract guard is `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs` (`context/foundation/test-plan.md:97`).
- rendered smoke guard is `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopRenderedTests.cs` (`context/foundation/test-plan.md:98`).
- browser/e2e is reserved for runtime behavior such as already-open-screen staleness or DOM interaction timing (`context/foundation/test-plan.md:105`).

The tests match that cookbook shape:

- `MonthlyBudgetingLoopTests` proves the monthly service projection, incomplete-balance branch, and scope boundary (`src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:25`, `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:238`, `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:261`).
- `MonthlyBudgetingLoopUiTests` reads source files directly and asserts page contracts without runtime browser cost (`src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:15`, `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:43`, `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:62`, `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:89`).
- `MonthlyBudgetingLoopRenderedTests` uses bUnit for a narrow service-provided rendered smoke, not full-screen e2e (`src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopRenderedTests.cs:9`, `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopRenderedTests.cs:15`).

Phase 2 cookbook (`section 6.2`) has the same cost/signal discipline for real-data readiness:

- policy/setup contract goes through `RealDataReadinessGateTests` (`context/foundation/test-plan.md:118`).
- live `/health/ready`, `pg_dump`, restore smoke, Render validation, and admin review stay manual unless the workspace has real external access (`context/foundation/test-plan.md:126`).
- browser/e2e and live Render automation should not be added merely to cover the external evidence gap (`context/foundation/test-plan.md:127`).

`RealDataReadinessGateTests` already provides a good local pattern for Phase 4: it reads repository files and asserts durable policy language and wiring (`src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs:5`, `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs:8`, `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs:32`, `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs:41`, `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs:53`). It also contains a reusable `ReadRepoFile` helper pattern (`src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs:77`).

Phase 3 cookbook (`section 6.3`) names the access restore boundary:

- `RecoveryBoundaryTests` is the main cross-component guard (`context/foundation/test-plan.md:140`).
- `AccessHardeningRedirectMiddlewareTests` is the routing guard (`context/foundation/test-plan.md:141`).
- isolated session/recovery service tests remain supporting coverage (`context/foundation/test-plan.md:142`).
- UI automation is explicitly unnecessary for `/access-recovery` route priority (`context/foundation/test-plan.md:148`).

The shipped tests match that contract:

- stale trusted cookie fails closed after recovery (`src/HouseholdBudgetMate.Tests/Tests/Services/RecoveryBoundaryTests.cs:30`).
- recovered admin can sign in and keeps default-user budget scope (`src/HouseholdBudgetMate.Tests/Tests/Services/RecoveryBoundaryTests.cs:64`).
- technical owner is excluded after recovery (`src/HouseholdBudgetMate.Tests/Tests/Services/RecoveryBoundaryTests.cs:94`).
- middleware redirects local recovery to `/access-recovery` and denies remote recovery requests (`src/HouseholdBudgetMate.Tests/Tests/Services/AccessHardeningRedirectMiddlewareTests.cs:51`, `src/HouseholdBudgetMate.Tests/Tests/Services/AccessHardeningRedirectMiddlewareTests.cs:72`).

### Phase 4 Oracle

The expected behavior for Phase 4 comes from the test-plan and 10x workflow rules:

- Tests should protect risk-first contracts, not chase coverage.
- Oracle must come from PRD/evidence/domain policy, not from implementation shape.
- The cheapest reliable signal wins: integration for real DB/service boundaries, hermetic tests for forced partial failures, static/component tests for surface contracts, browser/e2e only when cheaper layers cannot observe the risk.
- Selective AI-native review is a manual/local supplement for critical-screen consistency and cookbook smoke. It is not a CI gate and should not replace deterministic tests.
- A new quality gate should name owner/location, requiredness, command or manual evidence, triggering risk, and when not to use it.

`section 6.4` should therefore become decision guidance, not a new broad automation framework. It should cover:

- when to promote a shipped test pattern into `section 5 Quality Gates`;
- how to document the reference test file and exact command;
- when a gate is required, optional, local-only, or manual;
- how to keep manual evidence explicit for external dependencies;
- when to use Browser/AI-native review, and when to avoid it;
- how to update `section 6.5` with per-phase notes.

### Recommended Test Shape

Add a static repository contract test, likely under `src/HouseholdBudgetMate.Tests/Tests/Setup/TestPlanQualityGateTests.cs` or `src/HouseholdBudgetMate.Tests/Tests/Quality/TestPlanQualityGateTests.cs`.

Suggested assertions:

- `QualityGates_Should_Name_Shipped_Rollout_Gates`: `section 5` contains build/typecheck, unit + integration, targeted monthly-loop contract, real-data readiness contract, access restore/security regression tests, conditional e2e/browser, optional selective AI-native review.
- `Cookbook_Should_Record_Shipped_Reference_Tests`: `section 6.1`, `section 6.2`, and `section 6.3` each contain the relevant reference test files and no blanket e2e/browser advice.
- `QualityCookbook_Should_Define_Gate_And_AiNative_Decision_Rules`: `section 6.4` is not `TBD` and names deterministic-first, manual evidence, gate owner/location/requiredness/command, selective AI-native boundaries, and update rules for `section 5`/`section 6.5`.
- `RolloutNotes_Should_Be_Parseable`: phase notes contain `Phase 1 (...)`, `Phase 2 (...)`, `Phase 3 (...)`, and future `Phase 4 (...)` on a single line with no control-character or broken identifier artifacts.
- `Exclusions_Should_Preserve_Negative_Space`: `section 7` keeps OCR/file-upload exclusion, no broad visual snapshots, no broad e2e flows, and no coverage-padding tests.

This is cheap and high-signal because it guards the artifact that future agents will read before adding tests. It also catches the currently visible formatting issue in the Phase 3 note.

### Artifact Issue Found

Current `test-plan.md` has a broken Phase 3 note:

`context/foundation/test-plan.md:164` shows `- Phase 3 (` followed by a line break/control-character artifact before `ecovery-boundary-test)`.

Phase 4 should repair this as part of the cookbook/gate update and include a parseability assertion so the same class of issue is caught in the future.

## Code References

- `context/foundation/test-plan.md:49` - Phase 4 row and intended scope.
- `context/foundation/test-plan.md:72` - quality gate table.
- `context/foundation/test-plan.md:90` - monthly cookbook section.
- `context/foundation/test-plan.md:112` - real-data readiness cookbook section.
- `context/foundation/test-plan.md:134` - access restore/ownership cookbook section.
- `context/foundation/test-plan.md:156` - missing `section 6.4` section.
- `context/foundation/test-plan.md:168` - exclusions section.
- `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs:5` - static policy contract test pattern.
- `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs:5` - static UI/source contract pattern.
- `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopRenderedTests.cs:9` - narrow rendered smoke pattern.
- `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs:16` - service integration monthly contract.
- `src/HouseholdBudgetMate.Tests/Tests/Services/RecoveryBoundaryTests.cs:21` - recovery/session/scope integration boundary.
- `src/HouseholdBudgetMate.Tests/Tests/Services/AccessHardeningRedirectMiddlewareTests.cs:10` - middleware routing boundary.

## Architecture Insights

The existing test architecture supports Phase 4 without new dependencies:

- xUnit + FluentAssertions is already used across setup, UI contract, service, and architecture tests.
- Repository-file contract tests already exist and are accepted in the suite.
- bUnit exists, but Phase 4 does not need more rendered tests unless the plan introduces a runtime-visible cookbook UI, which it should not.
- Browser/AI-native review is available as a manual local tool but should remain outside CI unless a future risk proves deterministic layers insufficient.

The natural location is `Tests/Setup` because the test protects project setup and quality policy artifacts rather than application domain logic.

## Historical Context

Recent commits before this research:

- `e26c346` - recovery Phase 2 routing/security tests.
- `f21d050` - recovery Phase 1 boundary tests.

Earlier rollout changes established the pattern:

- cross-screen monthly consistency shipped service projection, static UI, and rendered smoke layers.
- real-data readiness shipped policy/setup tests plus manual evidence boundaries.
- recovery boundary shipped real-service session/recovery/scope coverage plus middleware routing tests.

Phase 4 should close the loop by making those patterns durable and test-guarded.

## Related Research

- `context/changes/testing-cross-screen-monthly-consistency/plan.md`
- `context/changes/real-data-readiness-gates/plan.md`
- `context/changes/recovery-boundary-test/plan.md`
- `context/foundation/test-plan.md`

## Open Questions

- The user has repeatedly requested commits without markdown files. Phase 4 inherently updates `context/foundation/test-plan.md`, so implementation should either keep markdown changes local and commit only code tests, or get explicit approval before committing markdown.
- If the plan chooses to add a `TestPlanQualityGateTests` file before fixing `test-plan.md`, the new test should intentionally fail until the cookbook and parseability fixes are applied.
