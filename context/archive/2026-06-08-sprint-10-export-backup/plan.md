---
date: 2026-06-07T18:05:00+02:00
planner: Codex
git_commit: 2636d136fb39b1707691c1663bfd1c96c6806bcf
branch: main
repository: HouseholdBudgetMate
topic: "Sprint 10 Export and Backup"
tags: [plan, export, backup, restore, admin, data-portability]
status: ready
last_updated: 2026-06-07
last_updated_by: Codex
---

# Implementation Plan: Sprint 10 Export and Backup

## Overview

Build data portability and backup safety for Household Budget Mate. The feature adds CSV export for monthly/yearly expenses and incomes, modular JSON backup export, full app JSON restore, scheduled automatic backups, and a new admin page at `/admin/backup`.

The highest-risk capability is restore. It must validate the backup, create a pre-restore backup, require typed confirmation, run in a transaction, preserve referential integrity through portable ID mapping, and avoid locking the household out of the app.

## Current State Analysis

- `ApplicationDbContext` owns all app data and enforces budget-owner scoping through `CurrentUserContext` and EF query filters (`src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:15`).
- Admin-only UI already exists in `AdminConfig.razor`, including admin session checks, status messages, `RunAdminActionAsync`, and destructive-operation confirmation (`src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor:1`).
- `MainLayout.razor` exposes admin navigation only for admin sessions and should add `/admin/backup` beside existing admin routes (`src/HouseholdBudgetMate.Web/Components/Layout/MainLayout.razor:101`).
- `CoreDataSeedService.ClearBudgetDataAsync` already clears budget data in dependency order while preserving profiles/config, but full restore needs a broader, safer contract (`src/HouseholdBudgetMate.Application/Helpers/CoreDataSeedService.cs:120`).
- The domain graph includes scoped budget entities, global taxonomy entities, visible users/PIN hashes, logs/audit, and dependent loan/expense/income records. Backup restore cannot rely on raw database IDs unless it controls identity insert.

## Requirements

### Functional Requirements

- Export CSV for expenses and incomes by selected month/year with filters.
- Export JSON backups as a single file with selectable sections.
- Support full app backup sections, including profiles and PIN hashes when selected.
- Restore JSON backup files, including full app restore.
- Before destructive restore, create an automatic pre-restore backup and require typed confirmation.
- Schedule automatic backups with configurable path, frequency, and local time.
- Add `/admin/backup` for admin backup/export/import management.

### Non-Functional Requirements

- Backup/restore must not expose budget data to non-admin users.
- Backup JSON v1 is plain JSON and must be labeled as sensitive.
- Restore must be transactional for relational databases.
- Import must validate schema version, sections, references, and at least one usable admin profile before replacing data.
- Automatic backup path must default to the app writable data directory and be configurable.
- CSV output must be deterministic, UTF-8, spreadsheet-friendly, and culture-stable.

## Decisions

| Area | Decision | Rationale |
| --- | --- | --- |
| Snapshot scope | Support whole-app backup, but with selectable sections in one JSON file. | Gives portability without forcing one uncontrolled dump shape. |
| Restore behavior | Replace after validation, pre-restore backup, and typed confirmation. | Clear mental model and safer destructive operation. |
| IDs | Use portable IDs with internal import mapping. | Avoids identity insert and cross-database ID collisions. |
| CSV scope | Expenses and incomes by selected period first. | Matches sprint requirement and current user workflow. |
| User profiles | Include profiles and PIN hashes when selected. | Enables full app restore without forced PIN reset. |
| Backup protection | Plain JSON v1 with explicit sensitive-file warnings. | Keeps restore inspectable and testable for this sprint. |
| Scheduling | Daily, weekly, or monthly at local time. | Covers household needs without cron-level complexity. |
| Verification | Service round-trip plus manual admin UI smoke. | Strongest signal for the risky data path without brittle browser automation. |

## Scope

### In Scope

- New application-layer backup/export service contract.
- JSON backup DTOs with manifest, version, sections, generated timestamp, app version metadata, and portable IDs.
- Sectioned export for:
- Budget data: categories, tags, accounts, balances, month plans, expenses, line items, savings transfers, incomes, recurring definitions, annual plans, loans, installments, rate entries, and charges.
- Admin/profile data: visible users, budget ownership, admin flags, household mode, and PIN hashes.
- Operational data: audit/log sections only if selected, clearly marked as optional.
- CSV export for expenses and incomes by selected period and filters.
- Full app restore from JSON.
- Automatic pre-restore backup.
- Scheduled automatic backup service and configuration.
- `/admin/backup` page and admin navigation.
- Service-level export/restore round-trip tests.
- Static UI contract tests for admin backup page labels and guardrails.

### Out Of Scope

- Encrypted/password-protected backup files.
- Cloud object storage targets.
- Cron-style custom schedules.
- Merge restore.
- Partial section restore as a user-facing operation unless needed internally for full restore.
- Browser automation for file download/upload.
- Public API backup endpoints.

## Architecture

Add a new backup/export feature slice across Abstractions, Application, Web, and Tests:

- `IBackupService` exposes CSV export, JSON backup export, backup validation/dry-run, and restore.
- `BackupService` uses `IDbContextFactory<ApplicationDbContext>`, `CurrentUserContext`, `IDateTimeProvider`, and logging.
- `BackupOptions` or runtime backup configuration stores backup path, schedule frequency, schedule local time, and enabled state.
- `ScheduledBackupService` runs periodic backups using `IHostedService` or `BackgroundService`.
- `AdminBackup.razor` provides admin-only controls at `/admin/backup`.
- Restore uses a single validated command object and performs replace in a database transaction.

## Backup Format Contract

### JSON Envelope

**Intent**: Make backups self-describing, versioned, and section-aware.

**Contract**: Add DTOs under `src/HouseholdBudgetMate.Abstractions/Contracts/Backup/`:

- `BackupEnvelopeDto`
- `BackupManifestDto`
- `BackupSectionSetDto`
- Section DTOs for budget, profiles, taxonomy, logs/audit, and metadata.

The envelope must contain:

- `schemaVersion`
- `applicationName`
- `createdAtUtc`
- `createdByUserId`
- `createdByUsername`
- `sections`
- `warnings`
- `payload`

### Portable IDs

**Intent**: Restore across databases without relying on original identity values.

**Contract**: Every exported entity that participates in relationships has a portable ID string. Import builds maps from portable IDs to new database IDs and uses those maps when creating dependent records.

### Sensitive Data Warning

**Intent**: Plain JSON v1 must be honest about risk.

**Contract**: Any backup containing profiles/PIN hashes must include a manifest warning stating that the file contains authentication secrets and household financial data.

## Phase 1: Backup Contracts And CSV Export

### Goal

Establish service contracts, JSON envelope shape, and CSV export for expenses/incomes.

### Changes Required

#### 1. Backup Contract DTOs

**File**: `src/HouseholdBudgetMate.Abstractions/Contracts/Backup/`

**Intent**: Define stable request/response types before implementation starts touching data.

**Contract**: Add request/response DTOs for backup section selection, CSV period/filter selection, backup export result, backup validation result, restore request, restore report, and schedule settings.

#### 2. Service Interface

**File**: `src/HouseholdBudgetMate.Abstractions/Interfaces/IBackupService.cs`

**Intent**: Keep backup operations behind an application service boundary, following existing `IExpenseService`, `IIncomeService`, and admin-service patterns.

**Contract**: Expose async methods for `ExportCsvAsync`, `CreateBackupAsync`, `ValidateBackupAsync`, `RestoreBackupAsync`, `GetBackupSettingsAsync`, `SaveBackupSettingsAsync`, and `RunScheduledBackupNowAsync`.

#### 3. CSV Export Implementation

**Files**:

- `src/HouseholdBudgetMate.Application/Services/BackupService.cs`
- `src/HouseholdBudgetMate.Application/Services/Backup/`

**Intent**: Provide deterministic spreadsheet exports for expenses and incomes by selected period and filters.

**Contract**: CSV export must include expenses and incomes for the active budget scope, using invariant delimiters/quoting and UTF-8 output. Expense rows should include year, month, expense name, category, tag, planned amount, actual amount, unplanned status, line-item description/amount/date when applicable, and soft-delete state only if explicitly requested. Income rows should include year, month, income name, amount, expected date, account, and recurring source where applicable.

#### 4. DI Registration

**File**: `src/HouseholdBudgetMate.Web/Program.cs`

**Intent**: Make backup service available to the admin page and background scheduler.

**Contract**: Register `IBackupService` and any backup settings/config services with scoped/singleton lifetimes appropriate to their dependencies.

### Automated Verification

- `dotnet build HouseholdBudgetMate.slnx -c Release`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~BackupServiceTests"`
- CSV tests assert stable headers, escaped values, expenses/incomes period filtering, and no data from unauthorized budget scope.

### Manual Verification

- Use a seeded local budget to export current month expenses and incomes.
- Open the CSV in a spreadsheet and confirm amounts, dates, category/tag labels, and line items are readable.

## Phase 2: JSON Backup Export

### Goal

Create a sectioned JSON backup file that can represent the full app and selected subsets.

### Changes Required

#### 1. Backup Snapshot Builder

**File**: `src/HouseholdBudgetMate.Application/Services/Backup/BackupSnapshotBuilder.cs`

**Intent**: Build a complete in-memory backup DTO from the current database with portable IDs.

**Contract**: The builder must support section selection and must include all selected entities needed for a referentially complete backup. Full app export includes visible users and PIN hashes when profile section is selected.

#### 2. Section Selection

**Files**:

- `src/HouseholdBudgetMate.Abstractions/Contracts/Backup/BackupSection.cs`
- `src/HouseholdBudgetMate.Web/Components/Pages/AdminBackup.razor`

**Intent**: Let admin choose a modular backup without juggling multiple files.

**Contract**: The UI and service both understand section flags such as `Budget`, `Profiles`, `Taxonomy`, `Audit`, `Logs`, and `SettingsMetadata`. The generated JSON file contains one envelope with only the selected sections.

#### 3. JSON Serialization

**File**: `src/HouseholdBudgetMate.Application/Services/Backup/BackupJsonSerializer.cs`

**Intent**: Keep JSON stable and testable.

**Contract**: Use `System.Text.Json` with explicit options for predictable property naming, date handling, decimals, and enum/string behavior. The serializer must reject unsupported schema versions during validation.

#### 4. Backup File Naming

**File**: `src/HouseholdBudgetMate.Application/Services/Backup/BackupFileName.cs`

**Intent**: Make manual and scheduled backup files easy to identify.

**Contract**: Names should include app prefix, timestamp, section summary, and extension, for example `household-budget-mate-backup-20260607-180500-full.json`.

### Automated Verification

- JSON export tests assert schema version, selected sections, sensitive warnings, stable portable IDs, and no missing relationship references.
- Full app export test includes profiles/PIN hashes only when profile section is selected.
- Budget-only export test excludes profiles, logs, and audit.

### Manual Verification

- Create a full backup from `/admin/backup`.
- Create a budget-only backup from `/admin/backup`.
- Inspect both JSON files and confirm the manifest accurately describes included sections and warnings.

## Phase 3: Full App Restore

### Goal

Safely restore a JSON backup, replacing current app data after validation, pre-restore backup, and typed confirmation.

### Changes Required

#### 1. Backup Validator

**File**: `src/HouseholdBudgetMate.Application/Services/Backup/BackupValidator.cs`

**Intent**: Fail before modifying the database when a backup is malformed or dangerous.

**Contract**: Validation checks schema version, required sections, duplicate portable IDs, missing references, invalid dates/months, invalid decimal values, and profile section safety. Full app restore must require at least one interactive admin profile or a documented recovery path.

#### 2. Restore Dry-Run Report

**Files**:

- `src/HouseholdBudgetMate.Abstractions/Contracts/Backup/BackupRestorePreviewDto.cs`
- `src/HouseholdBudgetMate.Web/Components/Pages/AdminBackup.razor`

**Intent**: Show the admin what will be replaced before confirmation.

**Contract**: Preview reports file metadata, selected sections, counts by entity type, warning list, and whether restore is allowed.

#### 3. Pre-Restore Backup

**File**: `src/HouseholdBudgetMate.Application/Services/Backup/BackupService.cs`

**Intent**: Ensure destructive restore has an automatic recovery point.

**Contract**: `RestoreBackupAsync` creates a timestamped full app backup before deleting or replacing data. If pre-restore backup fails, restore must fail.

#### 4. Restore Executor

**File**: `src/HouseholdBudgetMate.Application/Services/Backup/BackupRestoreExecutor.cs`

**Intent**: Replace current data while preserving relationships through portable ID maps.

**Contract**: For relational databases, restore runs inside one transaction. It clears affected tables in dependency order, inserts principals first, builds ID maps, inserts dependents, and commits only after all selected sections succeed. On failure, no partial restore should remain.

#### 5. Admin UI Confirmation

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/AdminBackup.razor`

**Intent**: Make destructive restore hard to trigger accidentally.

**Contract**: The UI must require selecting a JSON file, showing preview, and typing a confirmation phrase before calling restore. The phrase should include the target action, for example `RESTORE BACKUP`.

### Automated Verification

- Full app round-trip test exports a seeded app, clears/restores into a fresh database, and verifies users, admin role, budget data, categories/tags, accounts, expenses, line items, incomes, loans, annual plans, and projections.
- Restore validation tests reject missing references, unsupported schema versions, malformed sections, duplicate portable IDs, and no-admin profile sets.
- Restore failure test proves transaction rollback or no partial writes.

### Manual Verification

- Create a full backup, change visible data, restore the backup, and confirm the original data returns.
- Confirm a wrong confirmation phrase blocks restore.
- Confirm a malformed JSON file shows validation errors and does not change data.

## Phase 4: Scheduled Backups And Settings

### Goal

Add automatic backups with default app-data path, configurable path, frequency, and local time.

### Changes Required

#### 1. Backup Settings Storage

**Files**:

- `src/HouseholdBudgetMate.Web/Setup/RuntimeConfigurationState.cs`
- `src/HouseholdBudgetMate.Abstractions/Contracts/Backup/BackupSettingsDto.cs`

**Intent**: Store backup schedule and destination with existing runtime configuration patterns.

**Contract**: Settings include enabled flag, backup path, frequency (`Daily`, `Weekly`, `Monthly`), local time, last run, last status, and selected sections for scheduled backups. Default path is an app-data backup folder.

#### 2. Scheduled Backup Service

**File**: `src/HouseholdBudgetMate.Web/Setup/ScheduledBackupService.cs`

**Intent**: Run backups without user interaction.

**Contract**: Background service wakes periodically, checks whether a backup is due in local time, runs `IBackupService.CreateBackupAsync`, writes to configured path, and records result status. It must log failures without crashing the app.

#### 3. Admin Controls

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/AdminBackup.razor`

**Intent**: Let admins configure and test the schedule.

**Contract**: Page includes enabled toggle, path input, frequency selector, local time input, section selection for scheduled backups, last-run status, and `Run backup now`.

### Automated Verification

- Settings tests verify default path, save/load round-trip, path validation, and frequency validation.
- Scheduler tests verify due/not-due calculations for daily, weekly, and monthly schedules.
- Failure tests verify failed scheduled backup records status and does not crash.

### Manual Verification

- Configure daily backup path.
- Run backup now and verify a file appears in the selected folder.
- Confirm last-run status updates after success and after an intentionally invalid path.

## Phase 5: Admin Page Integration And Acceptance Evidence

### Goal

Complete `/admin/backup`, navigation, documentation/evidence, and final regression verification.

### Changes Required

#### 1. Admin Backup Page

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/AdminBackup.razor`

**Intent**: Provide one admin surface for export, backup, restore, and schedule management.

**Contract**: Route is `/admin/backup`. Non-admin users are redirected or shown access denied using the same pattern as `AdminConfig.razor`. The page labels backup files as sensitive, separates CSV export from JSON backup, and keeps restore visually distinct from export.

#### 2. Navigation

**File**: `src/HouseholdBudgetMate.Web/Components/Layout/MainLayout.razor`

**Intent**: Make backup management discoverable for admins only.

**Contract**: Add desktop and mobile admin navigation entries for `/admin/backup` beside audit/admin config links.

#### 3. Static UI Contract Tests

**File**: `src/HouseholdBudgetMate.Tests/Tests/Ui/BackupUiTests.cs`

**Intent**: Guard high-risk UI language and admin-only placement without brittle browser automation.

**Contract**: Assert the page contains sensitive backup warnings, typed confirmation text, pre-restore backup copy, schedule fields, and admin route. Assert main layout exposes backup navigation only within admin session blocks.

#### 4. Acceptance Evidence

**File**: `context/archive/2026-06-08-sprint-10-export-backup/acceptance-evidence.md`

**Intent**: Record automated and manual evidence for the Definition of Done.

**Contract**: Document commands, test results, manual CSV export, full backup export, restore smoke, scheduled backup smoke, and any remaining caveats.

### Automated Verification

- `dotnet build HouseholdBudgetMate.slnx -c Release`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`
- Static UI contract tests pass.
- Backup service round-trip tests pass.

### Manual Verification

- Admin can open `/admin/backup`.
- CSV export works for selected month/year.
- Full JSON backup downloads or writes successfully.
- Full restore returns the app to the backed-up state.
- Scheduled backup writes a file to the configured path.

## Testing Strategy

### Unit Tests

- Backup envelope serialization and schema version handling.
- Portable ID generation and relationship map validation.
- CSV escaping, headers, and period filters.
- Schedule due/not-due calculations.
- Backup settings validation.

### Integration Tests

- Full app export/restore round-trip using seeded data.
- Unauthorized budget scope isolation for CSV and JSON export.
- Restore validation rejects malformed or unsafe files.
- Restore transaction rollback on mid-import failure.
- Profile/PIN hash restore preserves at least one admin.

### Static UI Tests

- `/admin/backup` page includes sensitive-file warnings, destructive restore confirmation, and schedule controls.
- Admin navigation includes backup only inside admin-only blocks.

### Manual Tests

1. Export expenses and incomes CSV for a selected month.
2. Export full app backup and budget-only backup.
3. Restore full app backup after changing visible data.
4. Attempt restore with malformed JSON and verify no data changes.
5. Configure scheduled backup and run it immediately.

## Security Considerations

- Plain JSON backups are sensitive because they can include financial data and PIN password hashes.
- Only admin sessions may access `/admin/backup`.
- Restore must validate at least one usable admin before replacing profile data.
- Backup files must not be served through public static file middleware.
- Error messages should explain validation failures without dumping sensitive JSON content.
- Scheduled backup path validation must prevent writing into blocked/public directories.

## Performance Considerations

- Expected data volume is small, so simple in-memory DTO construction is acceptable for v1.
- Use async file IO for writing backup files.
- Avoid loading unrelated budgets unless the selected full app section explicitly requires profiles/app-wide data.
- Keep JSON readable; compression/ZIP is deferred.

## Migration Notes

- No database schema migration is required if backup settings live in runtime config.
- If later phases need persistent backup history in the database, add that as a separate migration-backed change.
- Existing data does not need transformation before enabling export.

## Rollback Strategy

- If CSV export fails, remove UI entry points and keep JSON backup work isolated behind service tests.
- If restore proves unsafe, ship export/scheduled backup first and keep restore hidden until round-trip tests pass.
- If scheduler causes startup issues, disable hosted service registration while preserving manual backup creation.

## References

- Sprint request: Sprint 10 Export + Backup.
- Admin UI pattern: `src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor`
- Admin navigation: `src/HouseholdBudgetMate.Web/Components/Layout/MainLayout.razor`
- Data scope and filters: `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs`
- Existing destructive data operation: `src/HouseholdBudgetMate.Application/Helpers/CoreDataSeedService.cs`
- Service round-trip testing pattern: `src/HouseholdBudgetMate.Tests/Tests/Services/MonthlyBudgetingLoopTests.cs`
- Static UI contract pattern: `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`

## Progress

### Phase 1: Backup Contracts And CSV Export

#### Automated

- [x] 1.1 Build passes after backup DTO/interface/service registration
- [x] 1.2 BackupService CSV tests pass for expenses/incomes, filters, escaping, and budget scope

#### Manual

- [x] 1.3 CSV export opens in a spreadsheet with readable expense and income rows

### Phase 2: JSON Backup Export

#### Automated

- [x] 2.1 JSON export tests pass for schema, selected sections, sensitive warnings, and portable IDs
- [x] 2.2 Budget-only export excludes profile/log/audit sections
- [x] 2.3 Full app export includes profile/PIN hash section only when selected

#### Manual

- [x] 2.4 Full and budget-only JSON files can be created and inspected from `/admin/backup`

### Phase 3: Full App Restore

#### Automated

- [x] 3.1 Full app export/restore round-trip test passes
- [x] 3.2 Restore validation rejects malformed, unsupported, duplicate, missing-reference, and no-admin backups
- [x] 3.3 Restore failure test proves no partial writes remain

#### Manual

- [x] 3.4 Full restore returns changed visible data to the backed-up state
- [x] 3.5 Wrong typed confirmation and malformed JSON do not change data

### Phase 4: Scheduled Backups And Settings

#### Automated

- [x] 4.1 Backup settings tests pass for defaults, save/load, and validation
- [x] 4.2 Scheduler due/not-due tests pass for daily, weekly, and monthly schedules
- [x] 4.3 Scheduled backup failure records status and does not crash the app

#### Manual

- [x] 4.4 Run backup now writes a JSON file to the configured path
- [x] 4.5 Invalid path shows failed status without crashing the app

### Phase 5: Admin Page Integration And Acceptance Evidence

#### Automated

- [x] 5.1 Full test suite passes
- [x] 5.2 Static backup UI contract tests pass
- [x] 5.3 Acceptance evidence records automated commands and results

#### Manual

- [x] 5.4 `/admin/backup` is usable by admin and blocked for non-admin
- [x] 5.5 Definition of Done is manually confirmed: full backup exported and restored correctly
