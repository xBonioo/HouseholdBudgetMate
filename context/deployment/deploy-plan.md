---
project: Household Budget Mate
planned_at: 2026-05-20T18:33:00+02:00
platform: Render
source_contract: context/foundation/infrastructure.md
stack_contract: context/foundation/tech-stack.md
deployment_mode: Blueprint-managed Docker web service plus Render Postgres
status: draft
---

# Render Deployment Plan

## Decision

Deploy Household Budget Mate to Render using the existing root-level `render.yaml` Blueprint.

The current repository is already aligned with Render:

- `render.yaml` defines `household-budget-mate-web` as a Docker web service and `household-budget-mate-db` as Render Postgres.
- `Dockerfile` publishes `src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj` and exposes port `10000`.
- `Program.cs` treats Render/container hosting as cloud runtime, reads Render's `PORT`, applies forwarded headers, skips HTTPS redirection in cloud, and uses `DATABASE_URL` for PostgreSQL.
- `PostgreSqlConnectionStringResolver` converts Render-style `postgres://` and `postgresql://` URLs into an Npgsql connection string.
- `Application__MigrateDatabaseOnStart=true` means the first Render boot applies EF Core migrations automatically when `DATABASE_URL` is present.

## Human Gates

1. Render account and workspace exist.
2. Repository is pushed to GitHub, GitLab, or Bitbucket and Render has access to it.
3. Before entering real household data, open the current readiness evidence record at `context/changes/secure-real-data-readiness/readiness-evidence.md` and confirm the final sign-off status.
4. The MVP may temporarily use Free Render as an explicitly accepted-risk pilot mode, but this is not durable production. Free Render Postgres has no Render-provided recovery or logical backups; use a local `pg_dump` backup before first real data and before every meaningful migration.
5. Before relying on quick mobile entry, decide whether to keep the free web service cold-start behavior or upgrade the web service.
6. Any migration that deletes, rewrites, or backfills meaningful data requires manual review, a fresh database backup, restore smoke-test notes, and rollback notes first.

## Pre-Deploy Local Checks

Run these before connecting Render:

```powershell
dotnet test HouseholdBudgetMate.slnx
docker compose up --build
```

Then verify locally:

- Open `http://localhost:10000`.
- Confirm the app starts without setup redirects when PostgreSQL is reachable.
- Confirm migrations complete and the app can create/read core household data.
- Stop the local stack after verification with `docker compose down`.

## Render CLI Setup

Install or update the Render CLI, then authenticate:

```powershell
render login
render workspaces
render workspace set
```

For non-interactive automation later, use `RENDER_API_KEY` in the environment instead of committing tokens.

## Blueprint Validation

Validate the checked-in Blueprint before creating production resources:

```powershell
render blueprints validate render.yaml
```

Expected result:

- YAML, schema, plans, regions, and resource references validate.
- `DATABASE_URL` resolves from `household-budget-mate-db`.
- `healthCheckPath: /health/ready` remains valid for the Blazor Server app and verifies PostgreSQL reachability once Phase 2 lands.

If validation fails on plan names, switch the production database plan in `render.yaml` to the paid Render Postgres plan chosen in the dashboard, then validate again.

## First Render Provisioning

Use Render Dashboard for the first Blueprint sync because Blueprint creation is reviewed visually before resources are created:

1. Open Render Dashboard.
2. Create a new Blueprint.
3. Select the repository and deployment branch.
4. Confirm Render detects the root `render.yaml`.
5. Review the resources:
   - `household-budget-mate-web`
   - `household-budget-mate-db`
6. Confirm region is `frankfurt`.
7. Confirm environment variables match `render.yaml`.
8. Deploy the Blueprint.

For a disposable smoke test, the current `plan: free` values can stay temporarily. For the MVP real-data pilot, Free Render is allowed only when `context/changes/secure-real-data-readiness/readiness-evidence.md` records the accepted risk, a manual `pg_dump` backup, and restore smoke-test notes. For durable production use, upgrade the database before the household enters real data.

## First Deploy Verification

After Render provisions resources, collect the resource IDs from the dashboard or CLI:

```powershell
render services
render deploys list <WEB_SERVICE_ID>
render logs --resources <WEB_SERVICE_ID> --tail
```

Verify:

- Web deploy reaches a successful state.
- Startup logs show EF Core migrations completed.
- No `Database migration failed on startup` critical log appears.
- The app responds at the Render `onrender.com` URL.
- `/health/ready` passes the configured health check.
- Setup flow is not shown in production when `DATABASE_URL` is present.
- Core user/household seed data exists as expected.

Database smoke query:

```powershell
render psql <POSTGRES_ID> -c "select current_database(), current_user;" -o text
```

## Deployment Commands After Provisioning

Normal deploy from the linked branch:

```powershell
render deploys create <WEB_SERVICE_ID> --wait
```

Deploy a specific Git commit:

```powershell
render deploys create <WEB_SERVICE_ID> --commit <COMMIT_SHA> --wait
```

Clear Render build cache only when build artifacts or SDK/runtime resolution look stale:

```powershell
render deploys create <WEB_SERVICE_ID> --clear-cache --wait
```

Use auto-deploy on merge only after the first successful deploy and database plan decision are complete.

## Production Readiness Adjustments

Before calling this a real production deployment:

- Change Render Postgres from free to a durable paid plan.
- Consider changing the web service from free to a paid instance to avoid cold starts and Blazor Server circuit interruption after idle sleep.
- Keep `docker-compose.yml` as local development infrastructure only; Render production remains `render.yaml`.
- Keep `HOUSEHOLDBUDGETMATE_DATA_DIR=/var/lib/householdbudgetmate`, but do not rely on that path for durable uploaded files unless Render persistent storage is explicitly added.
- Keep `Application__MigrateDatabaseOnStart=true` for the MVP only while migrations are reviewed before deploy. Revisit this once real data matters more than deployment speed.

## MVP Real-Data Pilot Gate

This project intentionally allows a narrower state than durable production: an MVP real-data pilot on Free Render. Treat it as accepted risk, not as provider-backed durability. The current readiness record for this gate is `context/changes/secure-real-data-readiness/readiness-evidence.md`; operators should review it together with the in-app admin readiness panel before entering real household data.

Before entering real household data on Free Render:

1. Record the decision in `context/changes/secure-real-data-readiness/readiness-evidence.md`.
2. Create a local logical backup with `pg_dump` from the Render external database URL.
3. Restore that dump into a non-production PostgreSQL database and record the smoke-test result.
4. Confirm `/health/ready` passes against the deployed app.
5. Confirm public `/files` serving is disabled until OCR/file upload enters scope.
6. Confirm the admin readiness panel shows the manual evidence items clearly.

Before a deploy that includes a meaningful migration after real data exists:

1. Review the migration for destructive operations, rewrites, or backfills.
2. Create a fresh `pg_dump` backup.
3. Record where the backup is stored and how restore was smoke-tested.
4. Record rollback or forward-fix notes before deploying.

## Rollback Plan

For app-code regressions:

1. Open the Render service Events page.
2. Roll back to the last successful deploy, or use the Render API rollback endpoint.
3. Confirm the app returns to a healthy state.

Important limits:

- App rollback does not roll back PostgreSQL schema or data.
- Environment variables and service configuration may remain at current values.
- A deploy of a newer commit can reintroduce the bad change if auto-deploy remains enabled.

For migration/data regressions:

1. Stop and assess before triggering more deploys.
2. Use the latest database backup/restore point recorded in `context/changes/secure-real-data-readiness/readiness-evidence.md`.
3. Apply a forward-fix migration only after manual review.

## Monitoring And Operations

Routine checks:

```powershell
render deploys list <WEB_SERVICE_ID>
render logs --resources <WEB_SERVICE_ID> --limit 100
render psql <POSTGRES_ID> -c "select count(*) from \"Users\";" -o text
```

Watch specifically for:

- Cold starts after idle periods.
- Blazor reconnect/circuit errors.
- EF migration failures.
- Npgsql connection retries and timeouts.
- Any writes to local filesystem paths that should be durable.
- Failed `/health/ready` checks.
- Missing backup/restore evidence before real-data migrations.

## Secrets Boundary

- Do not commit Render API keys, CLI tokens, deploy hooks, or database credentials.
- Prefer Render-managed `fromDatabase` references in `render.yaml`.
- Use local environment variables or dashboard-managed secrets for operational access.
- Human-only actions: delete database, rotate primary DB credentials, downgrade database plan, destructive migrations, and production data restore.

## References Checked

- Render CLI docs: https://render.com/docs/cli
- Render CLI reference: https://render.com/docs/cli-reference
- Render Blueprints docs: https://render.com/docs/infrastructure-as-code
- Render deploys docs: https://render.com/docs/deploys
- Render rollbacks docs: https://render.com/docs/rollbacks
