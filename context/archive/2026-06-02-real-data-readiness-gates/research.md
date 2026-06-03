---
date: 2026-06-02T13:12:07.2272164+02:00
researcher: Codex
git_commit: f494a78fa52c246d5ba1a227432d95e773663625
branch: main
repository: HouseholdBudgetMate
topic: "Rollout Phase 2: real-data readiness gates"
tags: [research, codebase, real-data-readiness, deploy, health, admin]
status: complete
last_updated: 2026-06-02
last_updated_by: Codex
---

# Research: Rollout Phase 2: real-data readiness gates

**Date**: 2026-06-02T13:12:07.2272164+02:00
**Researcher**: Codex
**Git Commit**: f494a78fa52c246d5ba1a227432d95e773663625
**Branch**: main
**Repository**: HouseholdBudgetMate

## Research Question

Ground rollout Phase 2 of `context/foundation/test-plan.md`: "Real-data readiness gates".

Verify risks #2 and #5:

- #2: The app presents real household data as safe to use before backup, restore, health, Render, and admin-readiness evidence are complete.
- #5: A deployment or migration changes PostgreSQL state while app rollback is mistaken for data rollback.

## Summary

The app already has a real-data readiness layer from `secure-real-data-readiness`: `/health/ready`, runtime safety options, disabled public `/files`, log retention checks, an admin readiness panel, and an evidence file. The remaining rollout risk is not "missing readiness implementation"; it is false approval: treating app-check readiness as final real-data approval while manual/external evidence is still pending.

The cheapest useful Phase 2 protection is a policy/integration contract around the existing readiness surfaces:

1. Assert `IsAppCheckReady` means only automated app checks, not real-data pilot approval.
2. Assert the admin panel keeps "automatic checks" separate from "manual evidence" and names Free Render as accepted risk rather than durable production.
3. Assert `readiness-evidence.md` cannot read as approved while restore smoke, live `/health/ready`, Render validation, admin panel review, or final sign-off are pending.
4. Assert deployment guidance keeps the database rollback boundary explicit: app rollback does not roll back PostgreSQL schema/data; meaningful migrations require `pg_dump`, restore smoke evidence, and migration review notes.

No browser/e2e is necessary for the core risk unless the plan chooses a manual visual admin-panel smoke. Static source/policy tests plus existing service/health tests provide higher signal.

## Detailed Findings

### Real-Data Approval Is Split Across App Checks And Manual Evidence

`RealDataReadinessService` builds an automated readiness report by checking database connectivity, public file serving, detailed errors, cookie hardening, log retention, and secure interactive administrator state (`src/HouseholdBudgetMate.Web/Setup/RealDataReadinessService.cs:22`). It returns manual items for accepted Free Render risk, manual `pg_dump`, restore smoke test, and migration review (`src/HouseholdBudgetMate.Web/Setup/RealDataReadinessService.cs:73`). The report's `IsAppCheckReady` property is only `AutomatedChecks.All(x => x.IsReady)` (`src/HouseholdBudgetMate.Web/Setup/RealDataReadinessService.cs:94`).

This is correct architecture, but it creates the exact testing target for risk #2: tests must prevent future UI/code from treating `IsAppCheckReady` as final real-data approval. Existing tests currently assert `report.IsAppCheckReady.Should().BeTrue()` when app checks pass while manual items still exist (`src/HouseholdBudgetMate.Tests/Tests/Setup/RealDataReadinessServiceTests.cs:23`). That is useful, but Phase 2 should add a stronger assertion that manual evidence remains separate and cannot be collapsed into app approval.

The admin page shows the distinction in UI: the chip says "App checks ready" or "App checks need attention" (`src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor:262`), the warning text states Free Render is an accepted MVP pilot risk (`src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor:268`), and the page has separate "Automatyczne kontrole" and "Ręczne evidence" sections (`src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor:278`, `src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor:296`). A static UI contract test is a good fit here.

### Evidence File Still Has Pending Manual Gates

The current F-02 evidence says automated implementation verification passed but external/manual real-data sign-off is pending (`context/changes/secure-real-data-readiness/readiness-evidence.md:5`). Backup-file evidence exists, but the source command/database could not be independently verified (`context/changes/secure-real-data-readiness/readiness-evidence.md:39`). Restore smoke test is still pending and must be completed against non-production PostgreSQL or explicitly accepted as a remaining blocker (`context/changes/secure-real-data-readiness/readiness-evidence.md:72`).

Runtime evidence has several manual pending items: live deployed `/health/ready` is pending (`context/changes/secure-real-data-readiness/readiness-evidence.md:78`), Render Blueprint validation is pending because Render CLI was unavailable (`context/changes/secure-real-data-readiness/readiness-evidence.md:80`), and admin readiness panel visual review is pending (`context/changes/secure-real-data-readiness/readiness-evidence.md:85`). Final real-data MVP pilot approval is still pending (`context/changes/secure-real-data-readiness/readiness-evidence.md:120`).

This makes `readiness-evidence.md` the best oracle for a policy test: if final sign-off is marked approved while any required evidence remains pending, the test should fail. Conversely, pending evidence should keep real-data approval unavailable without blocking ordinary app build/test gates.

### `/health/ready` Is Already Database-Aware And Privacy-Preserving

`ReadinessEndpoint` maps `/health/ready` (`src/HouseholdBudgetMate.Web/Setup/ReadinessEndpoint.cs:8`) and uses EF Core `CanConnectAsync` for the database check (`src/HouseholdBudgetMate.Web/Setup/ReadinessEndpoint.cs:24`). It returns only `{ status = "healthy" }` or `{ status = "unhealthy" }` with HTTP 503 on failure (`src/HouseholdBudgetMate.Web/Setup/ReadinessEndpoint.cs:43`), so it does not expose connection strings or exception text.

`ReadinessHealthTests` already covers healthy connectivity and a throwing database factory with a sensitive exception string (`src/HouseholdBudgetMate.Tests/Tests/Setup/ReadinessHealthTests.cs:13`, `src/HouseholdBudgetMate.Tests/Tests/Setup/ReadinessHealthTests.cs:24`). Phase 2 does not need to retest low-level database connectivity unless planning decides to add an HTTP-level endpoint test. The higher-value gap is live deployed evidence in the evidence file.

`render.yaml` already points Render's health check at `/health/ready` (`render.yaml:9`) and uses `fromDatabase` for `DATABASE_URL` (`render.yaml:28`). A static deployment contract test can protect those settings cheaply.

### Runtime Safety Gates Are Already Covered By Focused Tests

`RuntimeSafetyOptions` centralizes detailed error and public file serving flags (`src/HouseholdBudgetMate.Web/Setup/RuntimeSafetyOptions.cs:11`, `src/HouseholdBudgetMate.Web/Setup/RuntimeSafetyOptions.cs:17`). `appsettings.json` defaults `Blazor:DetailedErrors` to false and `FileStorage:EnablePublicFileServing` to false (`src/HouseholdBudgetMate.Web/appsettings.json:19`, `src/HouseholdBudgetMate.Web/appsettings.json:22`). `Program.cs` applies detailed error configuration to Blazor circuits (`src/HouseholdBudgetMate.Web/Program.cs:127`) and only maps public `/files` when the runtime option is enabled (`src/HouseholdBudgetMate.Web/Program.cs:346`).

Existing tests cover production detailed errors defaulting to false and public file serving defaulting to disabled (`src/HouseholdBudgetMate.Tests/Tests/Setup/RuntimeSafetyOptionsTests.cs:12`, `src/HouseholdBudgetMate.Tests/Tests/Setup/RuntimeSafetyOptionsTests.cs:48`). Cookie hardening is covered by a static App.razor source check for `SameSite=Strict` and HTTPS `Secure` (`src/HouseholdBudgetMate.Tests/Tests/Setup/SessionCookieHardeningTests.cs:7`). Operational log retention has service tests under `src/HouseholdBudgetMate.Tests/Tests/Services/OperationalLogCleanupServiceTests.cs:13`.

Phase 2 should not duplicate these checks broadly. It should use them as supporting gates and focus new assertions on readiness approval semantics and deploy/evidence policy.

### Migration And Rollback Risk Is Documented But Not Enforced By Tests

`Application__MigrateDatabaseOnStart` remains true in `render.yaml` (`render.yaml:17`) and `appsettings.json` (`src/HouseholdBudgetMate.Web/appsettings.json:13`). `Program.cs` runs EF migrations at startup when configuration allows it and an environment/runtime connection exists (`src/HouseholdBudgetMate.Web/Program.cs:281`), logging a critical error if migration fails (`src/HouseholdBudgetMate.Web/Program.cs:301`).

The deployment plan deliberately allows startup migrations for MVP but warns that migrations must be reviewed before deploy (`context/deployment/deploy-plan.md:151`). It also says app rollback does not roll back PostgreSQL schema/data (`context/deployment/deploy-plan.md:183`), and requires local `pg_dump` backup before first real data and before meaningful migrations (`context/deployment/deploy-plan.md:30`, `context/deployment/deploy-plan.md:169`).

This is a strong candidate for a policy/static test. The app cannot automatically prove external backups, but a test can keep the deployment contract from losing the rollback boundary, `pg_dump`, restore smoke, and migration review language.

## Code References

- `src/HouseholdBudgetMate.Web/Setup/RealDataReadinessService.cs:22` - Builds the readiness report from app-checkable gates.
- `src/HouseholdBudgetMate.Web/Setup/RealDataReadinessService.cs:73` - Manual evidence items: Free Render risk, `pg_dump`, restore smoke, migration review.
- `src/HouseholdBudgetMate.Web/Setup/RealDataReadinessService.cs:94` - `IsAppCheckReady` derives only from automated checks.
- `src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor:252` - Admin panel "Real data readiness" section starts.
- `src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor:262` - UI labels the automated state as "App checks ready", not final approval.
- `src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor:296` - Manual evidence section is separate from automated checks.
- `src/HouseholdBudgetMate.Web/Setup/ReadinessEndpoint.cs:8` - `/health/ready` path constant.
- `src/HouseholdBudgetMate.Web/Setup/ReadinessEndpoint.cs:43` - Sanitized healthy/unhealthy response shape.
- `render.yaml:9` - Render health check path is `/health/ready`.
- `render.yaml:17` - Startup migrations are enabled in Render env vars.
- `context/changes/secure-real-data-readiness/readiness-evidence.md:72` - Restore smoke test remains pending.
- `context/changes/secure-real-data-readiness/readiness-evidence.md:120` - Final real-data sign-off remains pending.
- `context/deployment/deploy-plan.md:183` - App rollback does not roll back PostgreSQL schema/data.

## Architecture Insights

The readiness architecture has two layers:

- App-check layer: deterministic, testable in code. Database connectivity, files disabled, detailed errors off, log retention enabled, cookie flags, secure admin.
- Human evidence layer: operational and external. Backup command/source, restore smoke, live health, Render Blueprint/workspace validation, admin panel review, migration review, final sign-off.

The rollout should not try to automate the human layer with fake confidence. The right test shape is a policy contract that proves the app and docs cannot present real-data approval unless the evidence layer is explicitly complete or explicitly accepted as a remaining blocker by a human.

`IsAppCheckReady` is a useful app-check signal, but it is intentionally not a real-data approval signal. That distinction should be named in tests and cookbook text.

## Historical Context

- `context/changes/secure-real-data-readiness/plan.md` established the readiness split: app-checkable items in code/admin UI and manual operational evidence in `readiness-evidence.md`.
- `context/changes/secure-real-data-readiness/readiness-evidence.md` records that automated verification passed, while restore smoke, live health, Render validation, admin panel visual review, and final sign-off remain pending.
- `context/deployment/deploy-plan.md` documents the MVP Free Render pilot gate and explicitly distinguishes app rollback from database rollback.
- `context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md:96` carries the same blocker forward: real-data evidence remains pending before real household data entry.

## Related Research

- `context/changes/secure-real-data-readiness/plan.md`
- `context/changes/secure-real-data-readiness/readiness-evidence.md`
- `context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md`
- `context/foundation/infrastructure.md`

## Open Questions

- Should Phase 2 introduce a small parser for `readiness-evidence.md`, or should it use static source-text assertions against required section/status language? A parser gives stronger semantics but may be too much abstraction for one policy artifact.
- Is a live `/health/ready` check available during implementation, or should the phase keep live health as manual evidence only?
- Should final real-data approval be represented in code at all, or remain purely in the evidence file/admin review process? Research recommends not adding a production feature unless planning identifies a user-visible approval workflow.
