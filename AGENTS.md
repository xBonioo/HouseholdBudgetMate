# Repository Guide

Household Budget Mate is a .NET web application for household budget planning. Keep this file concise: it is the entry point for agents, not the canonical product, architecture, testing, or change history.

## Start Here

- Product requirements: `context/foundation/prd.md`
- Roadmap and current product state: `context/foundation/roadmap.md`
- Architecture guide: `context/foundation/architecture/architecture-guide.md`
- Test strategy and cookbook: `context/foundation/test-plan.md`
- Tech stack: `context/foundation/tech-stack.md`
- Deployment plan: `context/foundation/deploy-plan.md`
- Active changes: `context/changes/`
- Completed changes: `context/archive/`
- Repository overview and local setup notes: `README.md`

## Commands

Restore packages:

```powershell
dotnet restore HouseholdBudgetMate.slnx
```

Build:

```powershell
dotnet build HouseholdBudgetMate.slnx
```

Run tests:

```powershell
dotnet test HouseholdBudgetMate.slnx
```

Run the main web app:

```powershell
dotnet run --project src/HouseholdBudgetMate.Web
```

Run local Docker stack:

```powershell
docker compose up --build
```

Format:

```powershell
dotnet format HouseholdBudgetMate.slnx
```

## Architecture Overview

- `src/HouseholdBudgetMate.Abstractions` contains public contracts, DTOs, requests, interfaces, and enums.
- `src/HouseholdBudgetMate.Domain` contains entities, EF configurations, and domain base types.
- `src/HouseholdBudgetMate.Migrations` contains `ApplicationDbContext` and EF Core migrations.
- `src/HouseholdBudgetMate.Application` contains application services, validation, mapping, and use-case logic.
- `src/HouseholdBudgetMate.Web` contains the Blazor Server UI, components, middleware, setup, and runtime configuration.
- `src/HouseholdBudgetMate.Tray` contains the local tray helper.
- `src/HouseholdBudgetMate.Installer` contains the MSI installer project.
- `src/HouseholdBudgetMate.Tests` contains service, setup, UI contract, and architecture tests.

## Stable Conventions

- UI calls application services; it must not access the database directly.
- Application services use `IDbContextFactory<ApplicationDbContext>` and create a context per operation.
- Domain entities are not returned to UI; use contracts from `HouseholdBudgetMate.Abstractions`.
- Mapping is explicit, usually in `HouseholdBudgetMate.Application/Mapping`.
- Request validation belongs in application services through FluentValidation.
- Time-dependent application logic should use `IDateTimeProvider`.
- Budget data must not be visible before a household profile is unlocked with PIN.
- `AuditLogs` are financial change history; operational log retention must not delete them.
- New tests should protect user-visible behavior or important technical boundaries, not mirror implementation details.

## Working With Context

- Keep detailed requirements, plans, research, decisions, test strategy, and evidence under `context/`.
- Keep root documentation lightweight and reference canonical files instead of duplicating them.
- Use `context/changes/<change-id>/` for active change-scoped plans, research, reviews, and evidence.
- Move completed changes to `context/archive/`.
- Do not create nested `AGENTS.md` files unless a future audit identifies module-specific rules that cannot live cleanly in the root guide or existing context docs.
