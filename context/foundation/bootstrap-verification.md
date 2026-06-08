---
bootstrapped_at: 2026-05-20T07:51:54Z
starter_id: dotnet
starter_name: ".NET (ASP.NET Core webapi)"
project_name: household-budget-mate
language_family: dotnet
package_manager: dotnet
cwd_strategy: subdir-then-move
bootstrapper_confidence: verified
phase_3_status: ok
audit_command: "dotnet list package --vulnerable --include-transitive"
---

## Hand-off

```yaml
---
starter_id: dotnet
package_manager: dotnet
project_name: household-budget-mate
hints:
  language_family: dotnet
  team_size: solo
  deployment_target: azure-app-service
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: verified
  path_taken: standard
  quality_override: false
  self_check_answers: null
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: false
  has_background_jobs: false
---
```

## Why this stack

Household Budget Mate is a small, after-hours web app with a 3-week MVP window, PIN-gated household access, and no MVP payments, AI, realtime transport, or background job requirement. The verified .NET starter fits the user's ASP.NET preference while keeping scaffolding smooth through a strongly typed, convention-based Microsoft stack with dependency injection, OpenAPI, and familiar tooling. Azure App Service is recorded as the starter's default deployment target because deployment was undecided; CI is GitHub Actions with auto-deploy on merge for the best-supported bootstrapper path. Jenkins was mentioned as a possible preference, but the hand-off schema only supports GitHub Actions, GitLab CI, CircleCI, and Cloudflare Builds.

## Pre-scaffold verification

| Signal | Value | Severity | Notes |
| --- | --- | --- | --- |
| npm package | not run | n/a | Non-JS starter; no npm create package is used by `dotnet new webapi`. |
| GitHub repo | not run | n/a | Card `docs_url` is `https://learn.microsoft.com/aspnet/core`, not a GitHub repository URL. |

## Scaffold log

**Resolved invocation**: `dotnet new webapi -n .bootstrap-scaffold --no-restore`
**Strategy**: subdir-then-move
**Exit code**: 0
**Files moved**: 6
**Moved paths**: `.bootstrap-scaffold.csproj`, `.bootstrap-scaffold.http`, `appsettings.Development.json`, `appsettings.json`, `Program.cs`, `Properties\launchSettings.json`
**Conflicts (.scaffold siblings)**: none
**.gitignore handling**: absent in scaffold
**.bootstrap-scaffold cleanup**: deleted

**CLI stdout**:

```text
Pomyślnie utworzono szablon "Internetowy interfejs API platformy ASP.NET Core".
```

**Post-bootstrap integration cleanup**: the generated root-level starter project was reviewed against the existing `src/HouseholdBudgetMate.Web` project and removed because it was only the stock weather-forecast API and was not part of `HouseholdBudgetMate.slnx`. Removed paths: `.bootstrap-scaffold.csproj`, `.bootstrap-scaffold.http`, `Program.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties\launchSettings.json`, and root `obj` restore artifacts.

## Post-scaffold audit

**Tool**: `dotnet list package --vulnerable --include-transitive`
**Summary**: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
**Direct vs transitive**: not distinguished by this tool output for this run

The audit was run twice for completeness:

- Existing solution from repo root: 0 vulnerable packages across `HouseholdBudgetMate.Abstractions`, `HouseholdBudgetMate.Application`, `HouseholdBudgetMate.Domain`, `HouseholdBudgetMate.Installer`, `HouseholdBudgetMate.Migrations`, `HouseholdBudgetMate.Tests`, `HouseholdBudgetMate.Tray`, and `HouseholdBudgetMate.Web`.
- Generated scaffold project directly: 0 vulnerable packages for `.bootstrap-scaffold.csproj`.

The first sandboxed attempts could not read user-level NuGet and SDK configuration, so the commands were rerun with filesystem access to the local .NET/NuGet configuration.

#### CRITICAL findings

None.

#### HIGH findings

None.

#### MODERATE findings

None.

#### LOW / INFO findings

None.

## Hints recorded but not acted on

| Hint | Value |
| --- | --- |
| bootstrapper_confidence | verified |
| quality_override | false |
| path_taken | standard |
| self_check_answers | null |
| team_size | solo |
| deployment_target | azure-app-service |
| ci_provider | github-actions |
| ci_default_flow | auto-deploy-on-merge |
| has_auth | true |
| has_payments | false |
| has_realtime | false |
| has_ai | false |
| has_background_jobs | false |

## Next steps

Next: a future skill will set up agent context (CLAUDE.md, AGENTS.md). For now, your project is scaffolded and verified.

Useful manual steps in the meantime:
- `git init` if you have not already, to start your own repo history.
- Review any `.scaffold` siblings the conflict policy created and decide which version of each file to keep.
- Address audit findings per your project's risk tolerance; the full breakdown is in this log.
