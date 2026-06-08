# Sprint 10 Export And Backup - Plan Brief

> Full plan: `context/changes/sprint-10-export-backup/plan.md`

## What & Why

Build data portability and backup safety for Household Budget Mate. Sprint 10 adds CSV export, modular JSON backups, full app restore, scheduled backups, and an admin management page so household data can be moved and recovered with confidence.

## Starting Point

The app already has scoped budget data through `CurrentUserContext` and EF query filters, admin-only UI patterns in `AdminConfig.razor`, and destructive data operations in `CoreDataSeedService`. It does not yet have export, backup, restore, or scheduled backup infrastructure.

## Desired End State

An admin can export expenses/incomes to CSV, create JSON backups with selected sections, restore a full app backup after validation and typed confirmation, and configure automatic backups to a local path. Restore creates a pre-restore backup first and preserves relationships through portable ID mapping.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Snapshot structure | One JSON file with selectable sections | Keeps backup movement simple while still supporting modular data sets. |
| Restore behavior | Replace after validation, pre-restore backup, and typed confirmation | Gives a clear and safer destructive restore model. |
| Entity IDs | Portable IDs with import mapping | Avoids database ID collisions and identity insert complexity. |
| CSV scope | Expenses and incomes by selected period | Matches the sprint requirement and current workflow. |
| Profile data | Include profiles and PIN hashes when selected | Enables true full app restore without forced PIN reset. |
| Backup protection | Plain JSON v1 with sensitive warnings | Keeps implementation inspectable and testable in this sprint. |
| Scheduling | Daily, weekly, or monthly at local time | Covers normal household backup needs without cron complexity. |
| Verification | Service round-trip plus manual admin UI smoke | Strong signal for restore correctness without brittle browser automation. |

## Scope

**In scope:**

- CSV export for expenses and incomes.
- Sectioned JSON backup export.
- Full app JSON restore.
- Automatic pre-restore backup.
- Scheduled backups with configurable path, frequency, and local time.
- `/admin/backup` page and admin navigation.
- Service-level round-trip tests and static UI contract tests.

**Out of scope:**

- Encrypted/password-protected backups.
- Cloud storage targets.
- Cron-style schedules.
- Merge restore.
- Browser automation for file upload/download.

## Architecture / Approach

Add a backup feature slice across Abstractions, Application, Web, and Tests. `IBackupService` owns CSV export, JSON backup, validation, restore, settings, and manual scheduled backup execution. `BackupService` and helper classes build sectioned envelopes with portable IDs, restore them transactionally, and write scheduled backups through a background service. `/admin/backup` reuses admin-only UI patterns from `AdminConfig.razor`.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Contracts and CSV | DTOs, service interface, CSV export | CSV shape must be stable and scoped correctly. |
| 2. JSON export | Sectioned backup envelope with portable IDs | Missing relationships would make restore unsafe. |
| 3. Full restore | Validated transactional restore with pre-restore backup | Data loss or admin lockout if validation is weak. |
| 4. Scheduled backups | Configurable automatic backups | Filesystem paths can be unavailable or ephemeral. |
| 5. Admin integration | `/admin/backup`, navigation, evidence | UI must make destructive restore unmistakable. |

**Prerequisites:** Admin profile access, configured database, writable app-data directory.
**Estimated effort:** About 4-6 focused implementation sessions across 5 phases.

## Open Risks & Assumptions

- Plain JSON full backups contain sensitive financial data and PIN hashes; v1 relies on warnings and filesystem/user discipline.
- Full app restore must preserve at least one usable admin profile or a recovery path.
- Local scheduled backup paths may be weak in container/cloud environments unless configured to persistent storage.
- Data volume is assumed small enough for in-memory snapshot DTOs.

## Success Criteria

- Admin can export expenses/incomes CSV for selected periods.
- Admin can export and restore a full JSON backup correctly.
- Scheduled backup writes valid JSON files to the configured path and records status.
