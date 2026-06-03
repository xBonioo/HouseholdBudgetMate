# Secure Real Data Readiness Implementation Plan

## Overview

Implement a minimal but enforceable real-data readiness gate for the Render-hosted MVP. The goal is not a full security redesign; it is to make the conditions for entering real household budget data explicit, visible to an administrator, backed by operational evidence, and supported by narrow runtime safeguards.

## Current State Analysis

The roadmap defines `F-02` as the foundation that makes durable and observable household-data use checkable before the full monthly loop is validated. The Render deployment exists, but it is currently configured as a free web service plus free PostgreSQL database. The team has chosen to keep Free Render for the MVP pilot, so this plan must treat data loss as an accepted risk with compensating manual backup and restore evidence rather than describing the environment as fully durable production.

The application already has improved access work in progress around the technical `default-user`: the current service code excludes that technical owner from interactive sign-in, PIN validation, and administration. This plan therefore depends on the `S-01` access gate outcome instead of reimplementing PIN-gated household access. Remaining readiness gaps are runtime and operational: `/` is the Render health check, public `/files` is mapped even though OCR/files are outside MVP, Blazor detailed errors are always enabled, trusted-session cookies are set by JavaScript without security flags, and log cleanup is configured but not implemented.

## Desired End State

An administrator can see a real-data readiness panel in the app that shows whether the MVP pilot is ready for real household data on Render. The panel makes the accepted Free Render risk explicit and checks or records the supporting evidence: database connectivity through `/health/ready`, public file serving disabled for MVP, trusted-session cookie hardening in place, production detailed errors disabled, log retention active, manual `pg_dump` evidence captured, restore smoke test recorded, and migration review/backup gates documented.

The Render Blueprint uses `/health/ready` for HTTP health checks. Automatic EF Core startup migrations may remain enabled for the MVP, but every meaningful real-data deploy or migration requires manual review, a fresh backup, and rollback notes in `readiness-evidence.md`. Public file serving remains disabled or blocked until the future OCR/file-upload scope deliberately adds authenticated file access.

### Key Discoveries:

- [roadmap.md](F:/Kamil/.Net/_projects/HouseholdBudgetMate/context/foundation/roadmap.md:73) defines `secure-real-data-readiness` as the foundation for safe real-data use before `S-02`.
- [render.yaml](F:/Kamil/.Net/_projects/HouseholdBudgetMate/render.yaml:6) and [render.yaml](F:/Kamil/.Net/_projects/HouseholdBudgetMate/render.yaml:31) configure both the web service and database on Free Render plans.
- [render.yaml](F:/Kamil/.Net/_projects/HouseholdBudgetMate/render.yaml:9) uses `/` as the current health check path, while [Program.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Program.cs:333) maps the Blazor app and controllers without a dedicated readiness endpoint.
- [Program.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Program.cs:128) always enables Blazor `DetailedErrors`.
- [Program.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Program.cs:341) maps the writable files directory to the public `/files` request path.
- [App.razor](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Components/App.razor:41) sets the remembered-session cookie with `SameSite=Lax` and without a `Secure` flag.
- [UserSessionService.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:61) already validates a session security stamp when restoring the remembered profile.
- [appsettings.json](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/appsettings.json:14) enables `LogCleanupTask`, but no cleanup consumer was found.
- [SerilogExtensions.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Application/Kernel/Extensions/SerilogExtensions.cs:46) persists operational logs to PostgreSQL when a connection string is present.
- [deploy-plan.md](F:/Kamil/.Net/_projects/HouseholdBudgetMate/context/deployment/deploy-plan.md:31) already states that meaningful data migrations require manual review and backup.
- Render documentation says Free Render Postgres has no Render-provided recovery/logical backups and recommends `pg_dump` from a local machine for free instances: [Render Postgres Recovery and Backups](https://render.com/docs/postgresql-backups).
- Render documentation supports HTTP health checks whose endpoint can run operation-critical checks such as a simple database query: [Render Health Checks](https://render.com/docs/health-checks).

## What We're NOT Doing

- Upgrading Render Postgres or the web service to a paid plan in this change.
- Claiming Free Render is durable production infrastructure.
- Reworking the full authentication model into ASP.NET Core cookie authentication.
- Reopening the `S-01` PIN-gated household-access plan, except to surface its outcome as a readiness dependency.
- Moving existing shared-budget records away from the internal technical owner.
- Adding OCR, receipt upload, document upload, authenticated file downloads, or persistent Render disks.
- Building full CI/CD deployment automation.
- Replacing EF startup migrations with a separate migration runner.
- Redacting existing audit history; audit remains available to administrators.

## Implementation Approach

Add a small readiness layer around the current Render MVP path. Keep Free Render as an explicitly accepted pilot risk, but require compensating manual backup/restore evidence before real data and before migrations. Add narrow runtime protections that are cheap and directly tied to the discovered risk: a database-aware readiness endpoint, production-safe Blazor error settings, hardened remembered-session cookie flags, disabled public `/files`, and log retention. Expose these checks in the existing admin area and record the manual evidence in a change-local evidence file.

## Critical Implementation Details

### Accepted Free Render Risk

The readiness panel and documentation must not say the MVP is "durable production" while the database remains on Free Render. The correct state is an accepted-risk real-data pilot backed by manual `pg_dump`, restore smoke testing, and migration review discipline.

### Public Files Boundary

The file storage service can remain in the codebase for future OCR work, but the public static-file mapping must be disabled or blocked for the MVP readiness state. If a future OCR change needs files, it must add authenticated download semantics instead of silently re-enabling public `/files`.

### Readiness Endpoint Privacy

`/health/ready` should return simple healthy/unhealthy status without leaking connection strings, schema details, exception messages, user data, or financial counts. Detailed diagnostics belong in server logs and the admin readiness panel, not in the public health response.

## Phase 1: Define Readiness Contract and Evidence Format

### Overview

Document the real-data gate, the accepted Free Render risk, and the evidence required before the household enters real budget data.

### Changes Required:

#### 1. Deployment Readiness Contract

**File**: `context/deployment/deploy-plan.md`

**Intent**: Update the deployment guidance from "upgrade before real data" to the user-approved MVP pilot contract: Free Render is allowed only with explicit risk acceptance and manual backup/restore evidence.

**Contract**: The deployment plan names Free Render as an accepted-risk MVP mode, requires `pg_dump` before first real data and before meaningful migrations, requires restore smoke-test notes, keeps migration review/backup before deploy, and points the health check to `/health/ready`.

#### 2. Render User-Facing Notes

**File**: `docs/render-deploy.md`

**Intent**: Align the public deployment notes with the current access model and real-data readiness rules.

**Contract**: Remove stale wording that says the Render deployment exposes a PIN-less `default-user`; document that the technical owner is non-interactive, real-data MVP use requires readiness evidence, and public file serving is disabled until OCR/file upload enters scope.

#### 3. Evidence Artifact Template

**File**: `context/changes/secure-real-data-readiness/readiness-evidence.md`

**Intent**: Create the manual evidence log the implementer will fill while validating readiness.

**Contract**: Include sections for accepted risks, backup command/output path, restore smoke-test notes, migration review notes, `/health/ready` result, Render Blueprint validation result, file-serving status, cookie-hardening status, log-retention status, and final human sign-off.

### Success Criteria:

#### Automated Verification:

- Readiness evidence artifact exists: `Test-Path context/changes/secure-real-data-readiness/readiness-evidence.md`
- Deployment documentation references `/health/ready`, `pg_dump`, restore smoke test, and accepted Free Render risk.

#### Manual Verification:

- User confirms the wording accurately distinguishes an accepted-risk Free Render MVP pilot from fully durable production.
- User confirms the evidence log captures enough information to decide whether real household data may be entered.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation that the readiness contract and accepted-risk language are correct before touching runtime behavior.

---

## Phase 2: Harden Runtime Safety Boundaries

### Overview

Add the runtime controls that make the readiness gate concrete: database-aware health, production-safe errors, hardened trusted-session cookie flags, and no public file serving for MVP.

### Changes Required:

#### 1. Database-Aware Readiness Endpoint

**File**: `src/HouseholdBudgetMate.Web/Program.cs`

**Intent**: Provide an application-level readiness endpoint Render can use to verify the web process and PostgreSQL dependency before routing traffic.

**Contract**: Add a `/health/ready` endpoint that performs a lightweight database connectivity check and returns a simple success/failure status without sensitive details. It should be usable as Render's `healthCheckPath`.

#### 2. Render Health Check Path

**File**: `render.yaml`

**Intent**: Make Render check the readiness endpoint instead of the root Blazor route.

**Contract**: Change `healthCheckPath` from `/` to `/health/ready`; keep the existing `DATABASE_URL` and migration environment variables unless later implementation discovers a conflict.

#### 3. Production Detailed Errors

**File**: `src/HouseholdBudgetMate.Web/Program.cs`

**Intent**: Prevent production Blazor circuits from exposing detailed exception information while preserving useful local diagnostics.

**Contract**: Configure `DetailedErrors` based on environment/configuration so production defaults to `false` and development can remain verbose.

#### 4. Trusted-Session Cookie Flags

**File**: `src/HouseholdBudgetMate.Web/Components/App.razor`

**Intent**: Keep the accepted 30-day trusted-session convenience while hardening browser cookie handling for HTTPS Render usage.

**Contract**: Update cookie JavaScript so remembered-session cookies use stricter same-site behavior and add `Secure` when the app is served over HTTPS. Preserve local HTTP development behavior where a `Secure` cookie would not be stored.

#### 5. Public Files Disablement

**File**: `src/HouseholdBudgetMate.Web/Program.cs`

**Intent**: Stop treating the writable app-data files folder as public web content during the MVP.

**Contract**: Gate or remove the static-file mapping for `Constants.RequestPathFiles`; default production/Render behavior must not expose `/files`. Any config flag that re-enables it should default to disabled and be documented as outside MVP.

### Success Criteria:

#### Automated Verification:

- Web project builds after runtime changes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- Targeted tests for readiness endpoint/session/file-serving behavior pass if introduced: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~Health|FullyQualifiedName~Session|FullyQualifiedName~File"`
- Render Blueprint validates with `/health/ready`: `render blueprints validate render.yaml`

#### Manual Verification:

- `/health/ready` returns healthy when the app can connect to the configured database and unhealthy when the database is unavailable.
- In HTTPS Render-style access, the remembered-session cookie is set with the intended security flags.
- `/files` is not publicly browseable or retrievable in MVP mode.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation that the runtime boundaries are acceptable before adding operational guardrails.

---

## Phase 3: Add Operations Guardrails

### Overview

Make backup, migration review, and log retention explicit and checkable, without replacing Render or building a full operations platform.

### Changes Required:

#### 1. Log Retention Execution

**File**: `src/HouseholdBudgetMate.Application/Kernel/Configurations/ApplicationConfiguration.cs`

**Intent**: Use the existing `LogCleanupTask` setting as a real retention control rather than a dormant configuration flag.

**Contract**: Preserve the existing setting and add any minimal configuration needed for retention age if the implementation requires it.

#### 2. Log Cleanup Worker or Service

**File**: `src/HouseholdBudgetMate.Application/Kernel/Extensions/SerilogExtensions.cs`

**Intent**: Prevent PostgreSQL operational logs from growing indefinitely during real-data use.

**Contract**: Implement or register a cleanup path that deletes old `Logs` rows according to configured retention when enabled. It must not delete `AuditLogs`, because audit remains the accepted financial change history.

#### 3. Sensitive Logging Rule

**File**: `docs/DOMAIN.md`

**Intent**: Capture the operational rule that new logs should avoid full financial payloads unless there is a specific diagnostic need.

**Contract**: Add a short logging/audit boundary: audit may retain financial diffs for administrators, operational logs should avoid new full financial payloads, and production detailed errors stay disabled.

#### 4. Backup and Migration Evidence Instructions

**File**: `context/changes/secure-real-data-readiness/readiness-evidence.md`

**Intent**: Give the implementer a concrete place to record manual `pg_dump`, restore smoke-test, and migration review evidence.

**Contract**: Include exact command slots for `pg_dump`, restore target, restore result, migration reviewed, backup created before migration, and rollback notes.

### Success Criteria:

#### Automated Verification:

- Log cleanup implementation compiles: `dotnet build HouseholdBudgetMate.slnx -c Release`
- Targeted log-retention tests pass if added: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~Log"`
- Full test suite passes after operational guardrails: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build`

#### Manual Verification:

- Evidence log records a successful `pg_dump` before real data or clearly marks the item pending.
- Evidence log records a restore smoke test into a non-production database or clearly marks the item pending with accepted risk.
- Migration review notes identify whether the current deploy includes schema/data changes and whether a backup exists.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation that the backup and migration-review workflow is practical for the MVP.

---

## Phase 4: Build Admin Readiness Panel

### Overview

Expose the readiness gate in the existing administration area so the decision is visible inside the application, not buried only in docs.

### Changes Required:

#### 1. Readiness Status Model

**File**: `src/HouseholdBudgetMate.Web/Setup/RealDataReadinessService.cs`

**Intent**: Centralize the app-checkable readiness state for the admin UI.

**Contract**: Add a small service that reports database reachability, health endpoint availability if practical, public-files setting, production detailed-error setting, remembered-session hardening status if representable, log-retention setting, and whether a secure interactive administrator exists through the current user service/access-hardening APIs.

#### 2. Admin Readiness UI

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor`

**Intent**: Show administrators the real-data checklist next to existing user/database administration.

**Contract**: Add a "Real data readiness" panel that distinguishes automated checks from manual evidence items. It must show Free Render as an accepted risk, link or point to `readiness-evidence.md`, and make manual items such as backup/restore/migration review visible without pretending the app can verify local backup files.

#### 3. Readiness Dependency on Access Gate

**File**: `src/HouseholdBudgetMate.Web/Setup/RealDataReadinessService.cs`

**Intent**: Keep `F-02` aligned with `S-01` without duplicating the PIN-access implementation.

**Contract**: The readiness model treats "secure interactive administrator/access gate present" as a dependency check. It must not revive interactive `default-user` access or implement the broader PIN flow.

### Success Criteria:

#### Automated Verification:

- Admin readiness service tests pass if introduced: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RealDataReadiness|FullyQualifiedName~AccessHardening"`
- Web project compiles with the admin panel: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual Verification:

- Admin page shows the readiness panel only to an admin session.
- Panel clearly separates app-checkable items from manual evidence items.
- Panel labels Free Render as accepted risk rather than durable production.
- Panel shows public `/files` as disabled for MVP and database readiness as healthy when PostgreSQL is reachable.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation that the admin checklist is understandable and not overstating what has been verified.

---

## Phase 5: Verify and Record Evidence

### Overview

Run the final automated checks and fill the evidence file so the change can be reviewed as a real-data gate rather than a set of unverified intentions.

### Changes Required:

#### 1. Final Evidence Capture

**File**: `context/changes/secure-real-data-readiness/readiness-evidence.md`

**Intent**: Record the concrete verification results for this change.

**Contract**: Fill the evidence log with build/test results, Render Blueprint validation result if available, `/health/ready` result, backup command/path or pending-risk note, restore smoke-test result or pending-risk note, migration review decision, and human sign-off status.

#### 2. Documentation Cross-References

**File**: `context/deployment/deploy-plan.md`

**Intent**: Make the deployment plan point future operators to the evidence artifact and admin checklist.

**Contract**: Add a reference to `context/changes/secure-real-data-readiness/readiness-evidence.md` as the current MVP real-data readiness record.

### Success Criteria:

#### Automated Verification:

- Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- Release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build`
- Git whitespace check passes: `git diff --check -- .`

#### Manual Verification:

- `/health/ready` is checked against a running app and database.
- `pg_dump` backup is created before real data or the evidence log explicitly marks that step pending before real data entry.
- Restore smoke test is performed against a non-production database or explicitly marked pending with accepted risk.
- Admin readiness panel is reviewed and understood by the user.

**Implementation Note**: This phase produces the final sign-off material. Do not mark the readiness gate complete if the evidence file says backup or restore verification is still pending before real data entry.

---

## Testing Strategy

### Unit Tests:

- Add focused tests for any readiness service that reports database/config/session/files/log-retention status.
- Add tests for log-retention cleanup logic if implemented as an injectable service.
- Keep existing `UserSessionService` tests green, and add coverage for any changed cookie/security-stamp assumptions that can be tested outside a browser.

### Integration Tests:

- Prefer lightweight application-level verification for `/health/ready` if the current test stack supports it without adding a large new framework.
- Use `dotnet build HouseholdBudgetMate.slnx -c Release` as the cross-project integration check for configuration and web wiring.
- Use `render blueprints validate render.yaml` when the Render CLI is available and authenticated.

### Manual Testing Steps:

1. Start the app against a reachable PostgreSQL database and confirm `/health/ready` returns a healthy status.
2. Stop or misconfigure the database and confirm `/health/ready` fails without exposing sensitive details.
3. Confirm the admin readiness panel reports database health, public file serving, log retention, Free Render risk, and manual evidence status clearly.
4. Confirm `/files` is not publicly accessible in MVP mode.
5. Confirm remembered-session cookie behavior still restores an eligible profile and uses the intended HTTPS security flags on Render-style access.
6. Run `pg_dump` against the Render database before real data or before a migration, record the backup path, and perform a restore smoke test into a non-production database.
7. Record migration review and rollback notes before any deployment that changes schema or data.

## Performance Considerations

The readiness endpoint must stay lightweight. A simple database connectivity check is sufficient; it should not count financial rows, load user data, or execute heavy queries. Log cleanup should run infrequently and delete by indexed timestamp or equivalent criteria to avoid affecting normal household usage.

## Migration Notes

No database schema migration is planned by default. If log retention requires an index or new table metadata, the implementer must treat that as a meaningful migration and record backup/review evidence before applying it to real data. Automatic startup migrations remain enabled for the MVP, but this plan adds manual backup and review gates around them.

## References

- Roadmap item: `context/foundation/roadmap.md` (`F-02`)
- Deployment plan: `context/deployment/deploy-plan.md`
- Render Blueprint: `render.yaml`
- Render Postgres recovery/backups: `https://render.com/docs/postgresql-backups`
- Render health checks: `https://render.com/docs/health-checks`
- Startup/runtime wiring: `src/HouseholdBudgetMate.Web/Program.cs`
- Session restore: `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs`
- Cookie JavaScript: `src/HouseholdBudgetMate.Web/Components/App.razor`
- Admin page: `src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Define Readiness Contract and Evidence Format

#### Automated

- [x] 1.1 Readiness evidence artifact exists: `Test-Path context/changes/secure-real-data-readiness/readiness-evidence.md`
- [x] 1.2 Deployment documentation references `/health/ready`, `pg_dump`, restore smoke test, and accepted Free Render risk

#### Manual

- [x] 1.3 User confirms the accepted-risk Free Render wording is correct
- [x] 1.4 User confirms the evidence log captures enough real-data readiness information

### Phase 2: Harden Runtime Safety Boundaries

#### Automated

- [x] 2.1 Web project builds after runtime changes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- [x] 2.2 Targeted readiness/session/file tests pass if introduced: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~Health|FullyQualifiedName~Session|FullyQualifiedName~File"`
- [ ] 2.3 Render Blueprint validates with `/health/ready`: `render blueprints validate render.yaml`

#### Manual

- [ ] 2.4 `/health/ready` reports healthy with database connectivity and unhealthy without it
- [ ] 2.5 Remembered-session cookie uses intended security flags on HTTPS
- [ ] 2.6 `/files` is not publicly retrievable in MVP mode

### Phase 3: Add Operations Guardrails

#### Automated

- [x] 3.1 Log cleanup implementation compiles: `dotnet build HouseholdBudgetMate.slnx -c Release`
- [x] 3.2 Targeted log-retention tests pass if added: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~Log"`
- [x] 3.3 Full release test suite passes after operational guardrails: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build`

#### Manual

- [x] 3.4 Evidence log records a successful `pg_dump` before real data or marks it pending
- [x] 3.5 Evidence log records a restore smoke test or marks it pending with accepted risk
- [x] 3.6 Migration review notes identify schema/data changes and backup status

### Phase 4: Build Admin Readiness Panel

#### Automated

- [x] 4.1 Admin readiness service tests pass if introduced: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RealDataReadiness|FullyQualifiedName~AccessHardening"`
- [x] 4.2 Web project compiles with the admin panel: `dotnet build HouseholdBudgetMate.slnx -c Release`

#### Manual

- [ ] 4.3 Admin page shows the readiness panel only to an admin session
- [ ] 4.4 Panel separates app-checkable items from manual evidence items
- [ ] 4.5 Panel labels Free Render as accepted risk rather than durable production
- [ ] 4.6 Panel shows public `/files` disabled and database readiness healthy when PostgreSQL is reachable

### Phase 5: Verify and Record Evidence

#### Automated

- [x] 5.1 Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- [x] 5.2 Release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build`
- [x] 5.3 Git whitespace check passes: `git diff --check -- .`

#### Manual

- [ ] 5.4 `/health/ready` is checked against a running app and database
- [x] 5.5 `pg_dump` backup is created before real data or marked pending before real data entry
- [x] 5.6 Restore smoke test is performed against a non-production database or marked pending with accepted risk
- [ ] 5.7 Admin readiness panel is reviewed and understood by the user
