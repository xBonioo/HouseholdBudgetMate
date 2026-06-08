# Sprint 10 Export and Backup Acceptance Evidence

## Scope

This evidence file tracks Sprint 10 Definition of Done: a full backup can be exported and restored correctly, with CSV export, sectioned JSON backup, restore safety, scheduled backups, and admin-only UI integration.

## Automated Verification

Final Phase 5 run on 2026-06-08:

- `dotnet build HouseholdBudgetMate.slnx -c Release` passed.
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release` passed: 382 passed, 0 failed, 0 skipped.
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~BackupServiceTests|FullyQualifiedName~BackupUiTests|FullyQualifiedName~BackupSettingsAndSchedulerTests"` passed: 31 passed, 0 failed, 0 skipped.

Known unrelated warning:

- `PlanPage.razor` still emits MudBlazor analyzer warning `MUD0002` for an existing `Dense` attribute. This warning predates the Phase 5 backup UI changes and does not fail the build.

## Implemented Evidence

- `/admin/backup` is an admin-only page with a non-admin redirect and access-denied fallback.
- CSV export is separated from JSON backup in the admin UI.
- JSON backup creation includes explicit sensitive-file warnings for plain JSON files.
- Restore is visually distinct from export, requires file selection, supports validation, requires the typed phrase `RESTORE BACKUP`, and calls the service path that creates a pre-restore backup copy.
- Scheduled backup controls expose enablement, path, frequency, local time, selected sections, last-run status, and `Run backup now`.
- Restore upload uses a visible drag-and-drop style file box before validation.
- Admin navigation exposes `/admin/config` and `/admin/backup` in one desktop admin dropdown, with mobile admin links grouped in the sidebar.
- Scheduled backup UI warns that background tasks only run while the application process is awake.

## Review Fix Evidence

Implementation review fixes completed on 2026-06-08:

- Full-app backups now include budget records for all budget owners, not only the current interactive budget owner.
- Restore now rejects non-full backups before deleting data.
- Restore preserves user-scoped `UserId` values during technical import instead of stamping every restored row to the technical owner.
- Restore clears data with query filters ignored, so soft-deleted or otherwise filtered records do not block a replace.
- User profiles are restored by upsert rather than bulk-deleting the self-referencing profile table, reducing lockout risk.
- Scheduled backups now run with all-budget-owner export semantics and are covered by a no-interactive-user regression test.
- `/admin/backup` restore validation now renders preview counts and blocks restore unless the preview is allowed.

Regression coverage added:

- Full-app backup/restore round-trips multiple budget owners.
- Budget-only backup restore is rejected before destructive delete.
- Restore preview returns counts and blocks non-full backups.
- Scheduled backup includes budget data without an interactive user session.

## Manual Verification Status

Manual browser verification was confirmed by the user on 2026-06-08:

- Admin can open `/admin/backup`.
- Non-admin users are blocked or redirected away from `/admin/backup`.
- CSV export works for a selected month/year and opens in a spreadsheet.
- Full JSON backup downloads or writes successfully.
- Full restore returns the app to the backed-up state.
- Scheduled backup writes a JSON file to the configured path.
- Invalid paths and malformed restore files fail safely without changing data.

## Caveats

- Backup JSON v1 is intentionally plain JSON. Files can contain household financial data, profile data, and PIN hashes, so they must be stored outside public directories.
- Scheduled backups are process-local. They are not guaranteed when the hosting platform sleeps or stops the app; use an external scheduler or keep-alive infrastructure for critical unattended backups.
- Browser automation for download/upload is out of scope for this sprint; the UI is covered by static contract tests plus manual smoke verification.
