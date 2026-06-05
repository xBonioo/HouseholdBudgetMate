# Test Plan

> Phased test rollout for this project. Strategy is frozen at the top; §6 grows as rollout phases ship.
> Last updated: 2026-06-03. Refresh with `/10x-test-plan --refresh` when top risks, stack, or negative space changes.

## 1. Strategy

1. **Cost × signal.** The cheapest test that gives a real signal for the risk wins. Do not promote to e2e because e2e "feels safer." Do not put a vision model on top of a deterministic visual diff that already catches the regression.
2. **User concerns are first-class evidence.** Risks anchored in "the team is worried about X, and the failure would surface somewhere in <area>" carry the same weight as PRD lines or hot-spot data.
3. **Risks are scenarios, not code locations.** This plan documents *what could fail* and *why we believe it's likely* — drawn from documents, interview, and codebase *signal* (churn, structure, test base). It does NOT claim to know which line owns the failure. That knowledge is produced by `/10x-research` during each rollout phase. If the plan and research disagree about where the failure lives, research is the ground truth.

Hot-spot scope used for likelihood weighting: `src/HouseholdBudgetMate.Web`, `src/HouseholdBudgetMate.Application`, `src/HouseholdBudgetMate.Domain`, `src/HouseholdBudgetMate.Abstractions`, `src/HouseholdBudgetMate.Tray`.

Hot-spot scan: 29 commits in the scoped app code over the last 30 days. Top likelihood signals: `src/HouseholdBudgetMate.Web/Components/Pages` — 112 changed-file hits; `src/HouseholdBudgetMate.Web/Components/Dialogs` — 16; `src/HouseholdBudgetMate.Web/Program.cs` — 10; `src/HouseholdBudgetMate.Web/Components/Layout` — 8; `src/HouseholdBudgetMate.Abstractions/Contracts/Loans` — 7; `src/HouseholdBudgetMate.Abstractions/Contracts/Users` — 7.

## 2. Risk Map

Risks are user/business failure scenarios, not test names. The Source column cites the evidence that surfaced the risk; it does not cite code anchors.

| # | Risk (failure scenario) | Impact | Likelihood | Source (evidence — not anchor) |
|---|--------------------------|--------|------------|----------------------------------|
| #1 | Cross-screen monthly state diverges after edits, so Plan, Accounts, Dashboard/Home, or Statistics tell different budget stories. | High | High | Phase 2 interview Q3/Q4; `context/foundation/roadmap.md` S-02; hot-spot dir `src/HouseholdBudgetMate.Web/Components/Pages` — 112 changed-file hits/30d |
| #2 | The app presents real household data as safe to use before backup, restore, health, Render, and admin-readiness evidence are complete. | High | High | `context/foundation/roadmap.md` open real-data question and In Progress note; `context/changes/secure-real-data-readiness/readiness-evidence.md`; `context/deployment/deploy-plan.md` Free Render pilot gate |
| #3 | Session restore or upgrade hardening succeeds on the happy path but leaks budget data or blocks a legitimate household after edge-case restore/recovery. | High | Medium | Phase 2 interview Q2; `context/foundation/roadmap.md` S-01; hot-spot signals in access/setup/admin surfaces |
| #4 | Monthly financial contract regresses toward stale Safe-to-spend wording or incomplete-balance behavior, making old assumptions look valid again. | Medium | Medium | `context/foundation/roadmap.md` F-01 superseded notes; `context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md` accepted no-Safe-to-spend scope |
| #5 | A deployment or migration changes PostgreSQL state while app rollback is mistaken for data rollback. | High | Medium | `context/foundation/infrastructure.md` risk register; `context/deployment/deploy-plan.md` migration gates; `context/changes/secure-real-data-readiness/readiness-evidence.md` pending restore and deploy evidence |
| #6 | A logged-in or remembered session can access data for the wrong household/profile because ownership and technical-owner boundaries drift. | High | Medium | `context/foundation/roadmap.md` access-control stream; Phase 2 interview Q2; abuse/security lens for auth and household budget data |

### Risk Response Guidance

| Risk | What would prove protection | Must challenge | Context `/10x-research` must ground | Likely cheapest layer | Anti-pattern to avoid |
|------|-----------------------------|----------------|--------------------------------------|-----------------------|-----------------------|
| #1 | One edited monthly scenario produces the same user-visible state across the key monthly screens. | Service-level totals imply every screen agrees. | Source of truth for month state, projection paths into each screen, fixture/source-of-truth data, existing UI contract coverage. | integration + component/UI contract | Copied production calculation as the oracle. |
| #2 | Real-data approval cannot appear complete while manual or live evidence is missing. | Automated tests equal real-data readiness. | Evidence states, admin readiness contract, live health boundary, manual approval state. | integration + manual smoke checklist | Pretending external evidence is fully automatable. |
| #3 | Invalid/stale restore and upgrade states fail closed without blocking a valid recovery path. | PIN happy path covers restore and upgrade behavior. | Session restore states, hardening route, recovery path, trusted-browser lifetime, technical-owner rule. | integration | Happy-path-only restore tests. |
| #4 | Old Safe-to-spend labels or expectations stay absent and incomplete balance is explicit. | Old names are harmless if hidden in tests or copy. | Current product contract, visible labels, incomplete-balance states, accepted S-02 evidence. | contract/UI text test | Re-testing a removed feature instead of guarding against its return. |
| #5 | Migration/deploy readiness requires backup/restore notes before meaningful data change. | App rollback protects database state. | Deployment gate, migration review record, backup evidence, restore smoke evidence, rollback/forward-fix boundary. | policy/contract test + manual gate | CI-only confidence for data rollback risk. |
| #6 | Cross-profile or technical-owner access attempts fail even with an otherwise valid session. | Logged-in means authorized for all budget rows. | Ownership model, interactive profile boundary, system/bootstrap exception, authorization failure behavior. | integration/security test | Over-mocking authorization and missing persisted ownership state. |

## 3. Phased Rollout

Each row opens one downstream change folder via `/10x-new`. Status moves left-to-right through the values below; the orchestrator updates Status as artifacts appear on disk. Completed rollout phases now live under `context/archive/`.

| # | Phase name | Goal (one line) | Risks covered | Test types | Status | Change folder |
|---|------------|------------------|---------------|------------|--------|---------------|
| 1 | Cross-screen monthly consistency | Prove monthly edits stay consistent across user-visible screens. | #1, #4 | integration + component/UI contract | complete | `context/archive/2026-06-02-testing-cross-screen-monthly-consistency` |
| 2 | Real-data readiness gates | Lock real-data pilot approval behind the correct evidence and manual gates. | #2, #5 | integration + policy/manual smoke | complete | `context/archive/2026-06-02-real-data-readiness-gates` |
| 3 | Access restore boundaries | Stress session restore, upgrade hardening, recovery, and ownership abuse cases. | #3, #6 | integration/security | complete | `context/archive/2026-06-02-recovery-boundary-test` |
| 4 | Quality cookbook and gates | Turn shipped patterns into cookbook entries and name required gates. | cross-cutting | docs/gate contract + selective AI-native review | complete | `context/archive/2026-06-02-quality-cookbook-and-gates` |

Status vocabulary: `not started`, `change opened`, `researched`, `planned`, `implementing`, `complete`.

## 4. Stack

| Layer | Tool / framework | Version | Note |
|-------|------------------|---------|------|
| runtime | .NET | 10.0 target framework | Blazor Server web app with application/domain/abstractions projects. |
| web UI | ASP.NET Core Blazor Server + MudBlazor | MudBlazor 9.4.0 | Primary user-visible monthly, access, and admin surfaces. |
| data | EF Core + PostgreSQL | EF Core 10.0.7, Npgsql EF Core 10.0.1 | Production path is PostgreSQL; tests use EF Core InMemory and SQLite. |
| tests | xUnit + FluentAssertions | xUnit 2.9.3, runner 3.1.5, FluentAssertions 7.2.1 | Meaningful suite: 30+ test files across services, setup, UI contract, and architecture. |
| coverage | coverlet.collector | 8.0.1 | Available in test project; coverage percentage is secondary to risk coverage. |
| architecture | NetArchTest.Rules | 1.3.2 | Existing architecture checks cover dependency direction and contracts. |
| browser/e2e | none yet | n/a | Use only if research proves component/integration tests cannot provide the signal cheaply. |
| AI-native | Browser tool / selective agent review | checked: 2026-06-02 | Use for selective critical-screen review or cookbook smoke only; do not use when deterministic assertions cover the risk. |

**Stack grounding tools (current session):**
- Docs: Context7 CLI — resolved `.NET` to `/dotnet/docs` and fetched current .NET testing guidance for `dotnet test`, xUnit project structure, and ASP.NET Core integration testing; checked: 2026-06-02.
- Search: none exposed in current session — no Exa/search MCP available via tool discovery; checked: 2026-06-02.
- Runtime/browser: Browser plugin/tool available — possible selective runtime verification layer for local Blazor screens, not used during plan writing; checked: 2026-06-02.
- Provider/platform: none exposed in current session — no Render/GitHub/database MCP available to this agent; checked: 2026-06-02.

## 5. Quality Gates

The full set of gates that must pass before a change reaches production. "Required for §3 Phase <N>" means the gate is enforced once that rollout phase lands; before that, the gate is `planned`.

| Gate | Where | Required? | Catches |
|------|-------|-----------|---------|
| build/typecheck | local + CI | required | C# compile/type drift across web/application/domain/test projects |
| unit + integration tests | local + CI | required | financial, access, readiness, service, setup, and architecture regressions |
| targeted monthly-loop contract | local + CI | required after §3 Phase 1 | cross-screen monthly state drift and stale contract wording |
| real-data readiness contract | local + manual evidence | required after §3 Phase 2 | false real-data approval, missing backup/restore/live-health evidence |
| access restore/security regression tests | local + CI | required after §3 Phase 3 | restore, upgrade, recovery, and ownership boundary regressions |
| e2e/browser critical flow | local/manual or future CI | planned only if research proves cheaper layers insufficient | DOM/runtime behavior that component or integration tests cannot observe |
| selective AI-native review | local/manual | optional after §3 Phase 4 | inconsistent critical-screen presentation missed by deterministic checks |

## 6. Cookbook Patterns

This section fills in as rollout phases ship. Each phase plan must end with a sub-phase that updates the relevant recipe with location, naming, reference test, and run command.

### 6.1 Adding a cross-screen monthly consistency test

Use this pattern when a monthly edit could make Plan, Dashboard/Home, Accounts, or Statistics tell different financial stories.

Reference tests:

- `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs` is the primary numeric guard. Extend the controlled monthly-loop scenario there when the risk is projection agreement after edits. Read the same service projections the screens use: `ExpenseService.GetMonthAsync`, `ExpenseService.GetDashboardSummaryAsync`, `IncomeService.GetLiveBalanceAsync`, and `ExpenseService.GetYearStatisticsAsync`.
- `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs` is the static screen-role contract. Use it to guard labels, service wiring, incomplete-balance guidance, and the absence of stale `Safe-to-spend` / `SafeToSpend` wording.
- `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopRenderedTests.cs` is the rendered smoke layer. Keep it narrow: render a service-provided monthly contract state, not full Plan/Home/Accounts/Statistics pages.

Decision rule:

- Use service projection integration for numeric agreement. Expected values must come from accepted evidence such as `context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md`, not from copied production formulas.
- Use static UI contract tests for screen roles. Plan/Home carry `Pozostalo w planie` and `Live balance`; Accounts carries live balance, account/savings/envelope context, and not the monthly plan KPI; Statistics carries annual/monthly finance rollups and does not require `Live balance`.
- Use rendered smoke tests for minimal rendered confidence around labels/state. Do not let this grow into a broad component platform.
- Use browser/e2e only when research proves the actual risk is runtime behavior that service/static/rendered tests cannot observe, such as already-open-screen staleness or DOM interaction timing.

Reference commands:

- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~MonthlyBudgetingLoopRenderedTests"`

### 6.2 Adding a real-data readiness gate test

Use this pattern when the risk is false real-data approval, missing backup/restore evidence, or rollback guidance that no longer names the PostgreSQL boundary.

Reference tests:

- `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessGateTests.cs` is the main policy/setup contract. Use it to guard readiness-evidence status, deployment rollback language, Render blueprint wiring, and admin panel wording.
- `src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessServiceTests.cs` is the app-check semantics guard. Use it to prove `IsAppCheckReady` still means automated app checks only, while manual evidence stays separate.
- `src/HouseholdBudgetMate.Tests/Tests/Setup/ReadinessHealthTests.cs` remains the endpoint behavior check for `/health/ready`. Keep it focused on database reachability and sanitized failure behavior.

Decision rule:

- Use policy/setup contract tests for evidence files, deploy docs, and admin wording. Keep the assertions semantic and fail-closed when required manual evidence is still pending.
- Use service tests for the app-check vs manual-evidence split. A passing app report is not final real-data approval.
- Keep live `/health/ready`, `pg_dump`, restore smoke, Render validation, and admin review as manual evidence unless the workspace actually has the external access and tooling to prove them.
- Do not add browser/e2e or live Render automation just to paper over the external evidence gap.

Reference commands:

- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RealDataReadinessGateTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RealDataReadinessServiceTests|FullyQualifiedName~ReadinessHealthTests"`

### 6.3 Adding an access restore or ownership boundary test

Use this pattern when recovery, remembered-session restore, upgrade hardening, or cross-profile access could leak budget data.

Reference tests:

- `src/HouseholdBudgetMate.Tests/Tests/Services/RecoveryBoundaryTests.cs` is the main cross-component guard. Use it to prove stale trusted-cookie invalidation, recovered admin sign-in, technical-owner exclusion, and restored budget-owner scope with real persisted users.
- `src/HouseholdBudgetMate.Tests/Tests/Services/AccessHardeningRedirectMiddlewareTests.cs` is the routing guard. Use it to pin recovery-priority redirects and remote denial when the recovery path is active.
- `src/HouseholdBudgetMate.Tests/Tests/Services/UserSessionServiceTests.cs` and `src/HouseholdBudgetMate.Tests/Tests/Services/AccessRecoveryServiceTests.cs` remain supporting coverage for isolated session and recovery behavior.

Decision rule:

- Use real `UserService`, `UserSessionService`, `AccessRecoveryService`, and persisted user rows for the stale-cookie/recovery boundary. A mock `IUserService` is fine for isolated session behavior, but not for the recovery-reset boundary where the session security stamp must come from persisted user state.
- Seed at least one `default-user` owned budget row when the risk includes wrong-household or wrong-profile data access.
- Use middleware tests for route priority and loopback denial. Do not drive the full UI just to prove `/access-recovery` wins over `/access-setup`.
- Keep the trust boundary tight: current valid visible profiles may restore, but a cookie minted before recovery must fail after the admin PIN changes.

Reference commands:

- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RecoveryBoundaryTests|FullyQualifiedName~AccessHardeningRedirectMiddlewareTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~UserSessionServiceTests|FullyQualifiedName~AccessRecoveryServiceTests|FullyQualifiedName~UserScopingTests|FullyQualifiedName~UserServiceAuthorizationTests"`

### 6.4 Adding a quality gate or selective AI-native review

Use this pattern when a shipped rollout phase has produced a repeatable testing rule that future changes must keep following. This is a deterministic quality-gate recipe first; browser/e2e and AI-native review stay outside the practical scope unless deterministic layers cannot observe the risk.

Reference tests:

- `src/HouseholdBudgetMate.Tests/Tests/Setup/TestPlanQualityGateTests.cs` is the local rollout-policy contract. Use it to guard the gate table, shipped cookbook recipes, rollout-note parseability, and negative-space rules.

Decision rule:

- Promote a gate only after a shipped risk pattern exists and the rollout has a concrete reference test or manual evidence path.
- Document owner/location, requiredness, command or manual evidence, and regression caught for every promoted gate.
- Keep manual evidence explicit when the risk depends on external systems such as Render, live PostgreSQL backup/restore, or human sign-off.
- Use browser/e2e only when deterministic layers cannot observe the risk, such as already-open-screen staleness or DOM interaction timing.
- Do not use AI-native review as a replacement for deterministic tests. If it is ever used, keep it advisory and document which deterministic gap it is supplementing.

Reference commands:

- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~TestPlanQualityGateTests"`

### 6.5 Per-rollout-phase notes

- Phase 1 (`testing-cross-screen-monthly-consistency`) shipped the cross-screen monthly consistency pattern. The cheapest useful protection was service projection integration plus static/rendered UI contracts, not browser/e2e. Browser scope remains reserved for future risks where an already-open screen fails to refresh or a real DOM interaction cannot be covered deterministically.
- Phase 2 (`real-data-readiness-gates`) shipped the real-data readiness gate pattern. The cheapest useful protection was policy/setup tests plus service-level semantics and manual evidence gates for risks #2 and #5, not browser/e2e or live Render automation.
- Phase 3 (`recovery-boundary-test`) shipped the access restore boundary pattern. The cheapest useful protection was real-service recovery/session/scope coverage plus a small middleware routing test, not UI automation.
- Phase 4 (`quality-cookbook-and-gates`) shipped the quality cookbook and gate pattern. The cheapest useful protection was a local static contract test for `context/foundation/test-plan.md` plus cookbook cleanup, not browser/e2e or AI-native review automation.
- S-03 (`improve-monthly-planning`) shipped the monthly preparation pattern. Use `ExpenseServiceTests` for suggestion/copy math and persistence boundaries, `MonthlyBudgetingLoopUiTests` for first-open/copy/Statistics wiring and no `Safe-to-spend` wording, `MonthlyBudgetingLoopTests` for projection regressions, and browser smoke only for Blazor interactions that static tests cannot click through.

### 6.6 Adding a monthly preparation planning test

Use this pattern when a change affects first-open month preparation, historical suggestions, targeted copy, annual planning, or Statistics alert-prep data.

Reference tests:

- `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs` is the primary behavior guard. Use it for preview-without-create, suggestion amount rounding, apply-with-edited-amount, recurring/loan duplicate suppression, explicit-target copy, annual plan persistence, and alert candidate math.
- `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs` is the static UI contract. Use it for PlanPage preparation wiring, apply/skip/copy handlers, editable suggestion fields, Statistics `Plan roczny`, alert-prep copy, and absence of stale `Safe-to-spend` / `SafeToSpend` wording.
- `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs` is the projection regression guard. Extend it only when preparation/copy changes affect the accepted monthly finance projection.

Decision rule:

- Service tests own math and persistence. Expected suggested amounts must be literal oracle values, not recomputed by copying production rounding helpers.
- Static UI tests own wiring and copy text. They are cheap guards for handler/service names and no-regression wording, not proof that MudBlazor interactions are comfortable.
- Browser smoke is required for first-open suggestion apply/skip, selected target copy, `Plan roczny` save/reload, and alert-prep presentation before final acceptance. Keep the notes in the change's `acceptance-evidence.md`.
- Keep scope tight: expense copy does not copy incomes, savings transfers, line items, or loan-backed rows; recurring and loan installment sync remain authoritative.

Reference commands:

- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`

## 7. What We Deliberately Don't Test

Exclusions agreed during the rollout. Future contributors should respect these unless the underlying assumption changes.

- **OCR/file upload paths** — parked outside MVP; do not spend rollout budget here until OCR, receipt upload, document upload, authenticated file downloads, or persistent file storage re-enters scope. Source: Phase 2 interview Q5 and roadmap parked items.
- **Full-page visual snapshots everywhere** — low signal for this risk map; use targeted component/contract assertions first and reserve visual review for 1-3 critical screens if deterministic checks miss presentation drift.
- **Coverage padding on generated or shape-only contracts** — existing architecture/DTO tests already cover broad contract shape; add risk-driven tests only when a user-visible behavior or boundary can fail.

## 8. Freshness Ledger

- Strategy (§1-§5) last reviewed: 2026-06-03
- Stack versions last verified: 2026-06-02
- AI-native tool references last verified: 2026-06-03

Refresh (`/10x-test-plan --refresh`) when:

- a new top-3 risk surfaces from the roadmap or archive;
- a tool's `checked:` date is older than three months;
- the tech stack changes;
- §7 negative space no longer matches what the team believes.


