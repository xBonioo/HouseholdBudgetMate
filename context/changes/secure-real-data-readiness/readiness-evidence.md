# Secure Real Data Readiness Evidence

> Change: `secure-real-data-readiness`
> Scope: Render MVP real-data pilot
> Status: automated implementation verification passed; external/manual real-data sign-off pending

## Decision Summary

- Free Render web service: accepted MVP pilot risk.
- Free Render Postgres: accepted MVP pilot risk.
- Durable production claim: not accepted while the database remains on Free Render.
- Compensation: manual `pg_dump`, restore smoke test, migration review, `/health/ready`, disabled public `/files`, and admin readiness checklist.

## Accepted Risks

| Risk | Accepted? | Notes | Confirmed by | Date |
| --- | --- | --- | --- | --- |
| Free Render Postgres has no Render-provided recovery/logical backups | Accepted for MVP pilot | Use local `pg_dump` before real data and before meaningful migrations. | User decision in plan | 2026-05-28 |
| Free Render web service can cold-start or interrupt Blazor Server circuits | Accepted for MVP pilot | Accept for MVP pilot or upgrade before relying on fast mobile entry. | User decision in plan | 2026-05-28 |
| JavaScript-managed remembered-session cookie cannot be `HttpOnly` | Accepted compromise | Keep 30-day trusted session as UX compromise; harden available cookie flags and security stamp. | User decision in plan | 2026-05-28 |

## Backup Evidence

### Before First Real Data

- Required before real household data: yes
- Command used:

```powershell
pg_dump "<RENDER_EXTERNAL_DATABASE_URL>" --format=custom --file ".\backups\household-budget-mate-<yyyyMMdd-HHmm>.dump"
```

- Backup file path:
- `C:\Users\bonif\Documents\pg_dump`
- Backup timestamp: file last write time `2026-05-30 12:19:20` local time.
- Operator: user supplied the artifact in this thread.
- Result: Backup artifact supplied. The file header starts with `PGDMP`, which matches a PostgreSQL custom-format dump; file size is 120095 bytes. Source database/command could not be independently verified in this workspace because no `RENDER_EXTERNAL_DATABASE_URL` environment variable is available.
- If pending, accepted-risk note and blocker: Treat this as backup-file evidence, but real-data approval still needs restore smoke-test evidence or explicit human acceptance of the remaining restore blocker.

### Before Meaningful Migration

- Migration or deploy identifier: `secure-real-data-readiness`
- Migration reviewed by: Pending final deploy review
- Destructive/rewrite/backfill operations found: No EF migration files were added by this change; final deploy review still required before applying to real data.
- Migration review result: Pending human deploy review
- Fresh backup command:

```powershell
pg_dump "<RENDER_EXTERNAL_DATABASE_URL>" --format=custom --file ".\backups\household-budget-mate-before-<migration-or-deploy>-<yyyyMMdd-HHmm>.dump"
```

- Backup file path:
- `C:\Users\bonif\Documents\pg_dump`
- Backup timestamp: file last write time `2026-05-30 12:19:20` local time.
- Result: Backup artifact supplied and format header verified as `PGDMP`; source database/command still not independently verified. No new EF migration file was added by the current implementation scope.
- Rollback or forward-fix notes: Use the latest backup recorded in this file; app rollback alone does not roll back PostgreSQL schema or data.

## Restore Smoke Test

- Restore target database:
- Restore command used:

```powershell
pg_restore --clean --if-exists --dbname "<NON_PRODUCTION_DATABASE_URL>" ".\backups\<backup-file>.dump"
```

- Smoke query or app check:
- Expected result:
- Actual result:
- Operator:
- Date:
- If pending, accepted-risk note and blocker: Restore smoke test is pending and must be completed against non-production PostgreSQL before real-data approval, or explicitly accepted as a remaining blocker. On 2026-05-30 this workspace had no `NON_PRODUCTION_DATABASE_URL` environment variable and `pg_restore` was not installed.

## Runtime Readiness Evidence

| Check | Expected | Evidence | Status |
| --- | --- | --- | --- |
| `/health/ready` with database reachable | 2xx/3xx response | Automated readiness DB check covered by `ReadinessHealthTests`; live deployed check pending because no live service URL/Render database URL was present in this workspace on 2026-05-30. | Pending manual |
| `/health/ready` with database unavailable | non-success response without sensitive details | `ReadinessHealthTests.CheckDatabaseAsync_Should_Report_Unhealthy_Without_Leaking_Exception` passed. | Verified by test |
| Render Blueprint health check path | `healthCheckPath: /health/ready` | `render.yaml` contains `healthCheckPath: /health/ready`; `render --version` failed on 2026-05-30 because the Render CLI is not installed in the current shell. | Pending CLI/manual |
| Public `/files` | Disabled or blocked in MVP mode | `FileStorage:EnablePublicFileServing` defaults false; `RuntimeSafetyOptionsTests` passed. | Verified by test |
| Remembered-session cookie hardening | HTTPS cookie uses intended security flags | `SessionCookieHardeningTests` passed for `SameSite=Strict` and HTTPS `Secure` flag. | Verified by test |
| Production detailed errors | Disabled in production | `Blazor:DetailedErrors` defaults false in Production; `RuntimeSafetyOptionsTests` passed. | Verified by test |
| Operational log retention | Enabled and implemented | `OperationalLogCleanupServiceTests` passed; cleanup deletes `Logs` only and preserves `AuditLogs`. | Verified by test |
| Admin readiness panel | App-checkable and manual evidence items visible to admins | `RealDataReadinessServiceTests` passed; visual admin review pending. | Pending manual |

## Automated Verification Results

| Check | Command | Result | Date |
| --- | --- | --- | --- |
| Release build | `dotnet build HouseholdBudgetMate.slnx -c Release` | Passed: 0 warnings, 0 errors | 2026-05-28 |
| Release tests | `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build` | Passed: 300/300 tests | 2026-05-28 |
| Git whitespace | `git diff --check -- .` | Passed: exit code 0; line-ending warnings only | 2026-05-28 |
| Render Blueprint validation | `render blueprints validate render.yaml` | Pending: Render CLI v2.18.0 runs, but validation stopped on missing workspace configuration | 2026-05-28 |
| Release build recheck | `dotnet build HouseholdBudgetMate.slnx -c Release` | Passed: 0 warnings, 0 errors | 2026-05-30 |
| Release tests recheck | `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release` | Passed: 306/306 tests | 2026-05-30 |
| Render CLI availability | `render --version` | Failed: `render` command is not installed in the current shell | 2026-05-30 |
| Backup artifact inspection | `Get-Item C:\Users\bonif\Documents\pg_dump` + first-byte inspection | File exists, size 120095 bytes, last write `2026-05-30 12:19:20`, header `PGDMP` | 2026-05-30 |
| Backup tool availability | `pg_dump --version` and `C:\Users\bonif\Documents\pg_dump --version` | Shell `pg_dump` is not installed; supplied `C:\Users\bonif\Documents\pg_dump` is a dump artifact, not the `pg_dump` executable | 2026-05-30 |
| Restore tool availability | `pg_restore --version` | Failed: `pg_restore` command is not installed in the current shell | 2026-05-30 |
| Required external environment variables | presence check for `RENDER_EXTERNAL_DATABASE_URL`, `NON_PRODUCTION_DATABASE_URL`, `RENDER_SERVICE_URL`, `RENDER_WORKSPACE_ID` | None were present in the current shell | 2026-05-30 |

## Admin Readiness Panel Evidence

- Admin panel reviewed by:
- Date:
- Shows Free Render as accepted risk: Pending manual UI review
- Separates automatic checks from manual evidence: Pending manual UI review
- Links or points to this evidence file: Implemented as evidence path text in the admin readiness panel; pending manual UI review
- Notes:

## Migration Review Notes

- Current deploy includes schema/data migration: No EF migration files added by this change.
- Migration files reviewed: No new EF migration file found in the current implementation scope; final deploy review still required before applying to real data.
- Backup created before deploy: Backup artifact supplied at `C:\Users\bonif\Documents\pg_dump`; source command/database not independently verified in this workspace.
- Restore smoke-tested: Pending before real-data approval.
- Rollback or forward-fix notes: App rollback does not restore PostgreSQL schema/data; use the latest backup recorded above and prefer forward-fix only after manual review.

## Final Sign-Off

- Real-data MVP pilot approved: Pending final human sign-off after backup/restore/admin-panel review
- Approved by:
- Date:
- Conditions or follow-up work:
