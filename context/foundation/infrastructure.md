---
project: Household Budget Mate
researched_at: 2026-05-20T00:00:00+02:00
recommended_platform: Render
runner_up: Railway
context_type: mvp
tech_stack:
  language: C# / .NET
  framework: ASP.NET Core Blazor Server
  runtime: Docker container on .NET 10
  database: PostgreSQL
---

## Recommendation

**Deploy on Render.**

Render is the selected MVP launch platform because the repository already contains a working `Dockerfile` and `render.yaml`, Render fully supports Docker-based web services, and Render Blueprints can define the web service plus PostgreSQL database in one file. This is not the cheapest durable option if kept entirely on free resources, because free Render Postgres expires after 30 days; the launch plan must upgrade the database or treat the free database as disposable. Sources: [Render Docker](https://render.com/docs/docker), [Render Blueprints](https://render.com/docs/blueprint-spec), [Render free limits](https://render.com/docs/free).

## Platform Comparison

Hard constraints:

- The app is ASP.NET Core Blazor Server targeting `net10.0`.
- The app already runs as a Docker web service.
- The data layer is PostgreSQL.
- Blazor Server uses long-lived SignalR/WebSocket connections even though the product does not have a separate realtime feature.
- Cost is a high priority; global edge deployment is not.

| Platform | Stack Fit | CLI-first | Managed / Serverless | Agent-readable docs | Stable deploy API | MCP / Integration | Cost fit | Result |
|---|---|---|---|---|---|---|---|---|
| Render | Pass | Pass | Pass | Partial | Pass | Pass | Partial | Shortlisted, selected |
| Railway | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Shortlisted, runner-up |
| Fly.io | Pass | Pass | Partial | Pass | Pass | Partial | Partial | Shortlisted |
| Azure App Service | Pass | Pass | Pass | Pass | Pass | Pass | Partial | Not shortlisted; heavier MVP surface |
| Cloudflare Workers / Pages | Fail | Pass | Pass | Pass | Pass | Pass | Pass | Dropped: no native ASP.NET Core container runtime |
| Vercel | Fail | Pass | Pass | Pass | Pass | Pass beta | Partial | Dropped: official function runtimes do not include .NET |
| Netlify | Fail | Pass | Pass | Partial | Pass | Pass | Partial | Dropped: functions are TypeScript, JavaScript, or Go |

Render scored well because it supports Docker services, Render Postgres, rollbacks, service previews, `render.yaml`, CLI validation, and a hosted MCP server for compatible coding agents. The cost caveat is real: free web services spin down on idle, free web services have ephemeral local filesystems, and free Postgres expires after 30 days. Sources: [Render Docker](https://render.com/docs/docker), [Render CLI](https://render.com/docs/cli), [Render MCP](https://render.com/docs/mcp-server), [Render free limits](https://render.com/docs/free).

Railway scored slightly better for low-cost durable MVP hosting because its Hobby plan is $5/month with $5 usage included and its Postgres service runs from a Docker-based Postgres image. It remains the best fallback if Render's database expiry or free-tier limitations become painful. Sources: [Railway pricing](https://docs.railway.com/pricing/plans), [Railway PostgreSQL](https://docs.railway.com/databases/postgresql), [Railway MCP](https://docs.railway.com/cli/mcp).

Fly.io is a strong Docker-first option with `fly deploy`, Machines, volumes, and fine-grained pricing. It is a worse first launch choice here because persistent Postgres on Fly requires more operational discipline around volumes, backups, and recovery. Source: [Fly deploy](https://fly.io/docs/flyctl/deploy/).

Azure App Service matches the original stack hint and supports custom containers, Azure CLI, Azure Developer CLI, Azure MCP, and managed PostgreSQL. It is not shortlisted because the MVP is cost-sensitive and after-hours; Azure adds more resource types, IAM, pricing, and portal/CLI concepts than this launch needs. Sources: [Azure App Service pricing](https://azure.microsoft.com/en-us/pricing/details/app-service/linux/), [Azure custom containers](https://learn.microsoft.com/en-gb/azure/app-service/quickstart-custom-container?tabs=dotnet), [Azure MCP](https://learn.microsoft.com/en-us/azure/developer/azure-mcp-server/overview).

## Shortlisted Platforms

### 1. Render (Recommended)

Render wins as the user-selected launch platform because the repo already has Render infrastructure-as-code, the web app already has a Dockerfile, and the application already reads Render-style `DATABASE_URL`. It is the fastest route from this repository to a public MVP as long as the database is upgraded before relying on real household data.

### 2. Railway

Railway is the runner-up and the stronger low-cost durable option. It supports ASP.NET Core, Dockerfiles, PostgreSQL, CLI logs with JSON output, deployment listing, and a first-class MCP setup for Codex. The main gap is that choosing Railway would mean writing new deployment config instead of using the existing `render.yaml`.

### 3. Fly.io

Fly.io is the most flexible container platform in the shortlist. It is attractive for Docker-first teams and single-region hosting, but for this MVP it asks the solo developer to think harder about volumes, Postgres backups, app machines, and rollout behavior.

## Anti-Bias Cross-Check: Render

### Devil's Advocate - Weaknesses

1. Free Render Postgres expires after 30 days, so a "free production" launch can silently become a data-loss risk if the database is not upgraded in time.
2. Render is not a raw `docker-compose.yml` host. The app container can run from the repo `Dockerfile`, but Postgres should be represented as Render Postgres in `render.yaml`, not as a user-managed compose service.
3. Free web services spin down after 15 minutes of inactivity and take about a minute to wake; this is rough for a household app that may be opened quickly from mobile during expense entry.
4. Blazor Server depends on a stable SignalR circuit. Deploys, restarts, free-tier sleep, or maintenance can disconnect active sessions.
5. Automatic database migrations on startup make deployment convenient, but image rollback does not undo schema or data changes.

### Pre-Mortem - How This Could Fail

The team launches Household Budget Mate on Render because the repository already has `render.yaml` and the first deploy works quickly. For the first week, the app feels fine, but the free web service sleeps between household check-ins, so mobile quick entry sometimes starts with a long cold wake. The free Postgres expiration email arrives during a busy week and is ignored because the app still "looks live." Thirty days later the database becomes inaccessible, and the household loses trust in the tool because budget history was not treated as production data. A later deploy introduces an Entity Framework migration bug; Render rollback restores the previous app image, but the changed database schema remains. The team then has to recover manually without a rehearsed backup/restore process. Render was workable, but the decision failed because the free tier was treated as durable infrastructure and because deployment rollback was mistaken for full application-state rollback.

### Unknown Unknowns

- Render launch uses `render.yaml`, not `docker-compose.yml`; the existing `postgres` compose service is local-dev infrastructure, not the production database definition.
- Render free web services lose local filesystem changes after redeploy, restart, or spin-down; household-uploaded files must live in durable storage if that feature matters.
- Free web services support rollbacks only to the two most recent previous deploys.
- Render's hosted MCP server is useful, but API keys are broadly scoped to accessible workspaces, so production access should be handled carefully.
- A paid database may be needed much earlier than expected because the first real household budget data is production data, not test data.

## Operational Story

- **Preview deploys**: Use Render service previews for single-service changes. If multi-service preview environments are needed later, configure Blueprint previews with `previews.generation`, but keep them manual or short-lived to avoid surprise compute cost.
- **Secrets**: Store production configuration in Render environment variables and database credentials generated by Render Postgres. Do not commit secrets to `render.yaml`; keep only references such as `fromDatabase` in version control.
- **Rollback**: Use Render Dashboard Events or the Render API rollback endpoint to roll back to a previous successful deploy. Free web services roll back only to the two most recent previous deploys. Database migrations and data changes do not roll back automatically.
- **Approval**: An agent may validate `render.yaml`, inspect logs, and trigger non-destructive deploys. A human must approve production database upgrade, database deletion, primary secret rotation, and any migration that changes or deletes data.
- **Logs**: Use `render deploys list <SERVICE_ID>` for deploy history and Render CLI service log views for runtime logs. Render MCP can also list services, deploys, logs, metrics, and run read-only database queries when configured with scoped operational intent.

## Risk Register

| Risk | Source | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| Free Postgres expires after 30 days | Devil's advocate | High | High | Upgrade Render Postgres before entering real household data, or treat free DB as throwaway demo data only. |
| Free web service cold starts hurt mobile quick entry | Devil's advocate | Medium | Medium | Move the web service to a paid instance before real household use, or document cold starts as demo-only behavior. |
| Render is not `docker-compose` production hosting | Unknown unknowns | Medium | Medium | Keep `docker-compose.yml` for local dev and make `render.yaml` the production contract. |
| Startup EF migrations break production data | Pre-mortem | Medium | High | Review generated migrations before deploy, back up DB first, and run destructive migrations only after human approval. |
| App rollback does not roll back DB state | Research finding | Medium | High | Pair every production deploy with a backup/restore point and keep migration rollback notes. |
| Blazor Server circuit disconnects during deploy or sleep | Devil's advocate | Medium | Medium | Use paid service before real use, keep one web instance initially, and make forms resilient to refresh/retry. |
| Broad Render MCP/API key access | Unknown unknowns | Low | High | Use least-privilege operational accounts where possible, avoid committing tokens, and reserve destructive actions for humans. |

## Getting Started

1. Install or update the Render CLI and authenticate with `render login`.
2. Validate the existing Blueprint with `render blueprints validate render.yaml`.
3. In Render, create/sync the Blueprint from the GitHub repository so it provisions `household-budget-mate-web` and `household-budget-mate-db` from `render.yaml`.
4. After the first deploy, inspect startup logs and confirm the app received `DATABASE_URL`, migrated successfully, and responds on `/`.
5. Before real household data is entered, upgrade Render Postgres from the free instance or replace it with another durable database plan.

## Out of Scope

The following were not evaluated in this research:

- Docker image redesign
- CI/CD pipeline setup
- Production-scale architecture, including multi-region HA and disaster recovery
