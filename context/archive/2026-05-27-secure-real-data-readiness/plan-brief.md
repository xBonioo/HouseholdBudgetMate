# Secure Real Data Readiness - Plan Brief

> Full plan: `context/changes/secure-real-data-readiness/plan.md`

## What & Why

This plan creates the `F-02` gate for entering real household budget data into the Render-hosted MVP. The goal is a practical readiness boundary: the app and deployment docs must clearly show what is safe, what is accepted risk, and what evidence exists before real financial data is trusted.

## Starting Point

The Render deployment is already defined with a Docker web service and Render Postgres, but both are on Free Render plans. Access hardening has moved the technical `default-user` out of interactive login, so this plan focuses on real-data operations: backup/restore, health, session hardening, public files, logs, and visible admin readiness.

## Desired End State

An administrator can open the app and see a real-data readiness checklist. The Render service checks `/health/ready`, public `/files` is disabled for MVP, production detailed errors are off, remembered-session cookies are hardened, logs have retention, and manual backup/restore/migration evidence is recorded in the change folder.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| Scope | Readiness gate plus minimal enforcement | Gives a checkable gate without turning this into a full security redesign. |
| Environment | Render production only for MVP | Matches the infrastructure decision and avoids broadening into local LAN/MSI readiness. |
| Database plan | Free Render accepted for MVP | The user accepts the cost/risk tradeoff, so the plan compensates with manual backup and restore evidence. |
| Migrations | Startup migrations stay with backup/review gate | Keeps MVP deployment simple while requiring a backup and review before meaningful data changes. |
| Backup | Manual `pg_dump` before real data and migrations | Free Render has no provider recovery, so the team needs its own recoverable dump. |
| Health | `/health/ready` checks app plus DB | Root-page health is too weak for a budget app whose critical dependency is PostgreSQL. |
| Session | Keep remembered session but harden flags | Preserves household UX while reducing browser-cookie risk. |
| Files | Disable/block public `/files` for MVP | OCR/file upload is outside MVP, so public file exposure should not be part of real-data readiness. |
| Logs | Retention plus no new sensitive operational logs | Audit remains useful, while operational logs stop growing forever and avoid unnecessary financial payloads. |
| UI | Admin readiness panel/checklist | Makes readiness visible in the product instead of only in docs. |
| Evidence | Code, docs, and `readiness-evidence.md` | Backup/restore and risk acceptance need manual proof beyond automated tests. |

## Scope

**In scope:**

- Render MVP real-data readiness contract.
- `/health/ready` and Render health-check update.
- Session cookie flag hardening for the existing remembered profile flow.
- Disable/block public `/files` for MVP.
- Log retention and production detailed-error safety.
- Admin readiness panel.
- `readiness-evidence.md` for backup, restore, migration review, and sign-off.

**Out of scope:**

- Paid Render upgrade.
- Full ASP.NET auth redesign.
- OCR, file upload, and authenticated file download.
- Moving shared-budget records away from the internal technical owner.
- CI/CD automation or a separate migration runner.

## Architecture / Approach

The plan wraps the existing Render deployment with a readiness layer. App-checkable items live in code/config and surface through the admin panel; manual operational evidence lives in `context/changes/secure-real-data-readiness/readiness-evidence.md`. Free Render remains an accepted-risk pilot mode, not a durable-production claim.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Define contract | Docs and evidence template | Wording could overstate Free Render durability. |
| 2. Harden runtime | Health endpoint, cookie flags, files disabled, safe errors | Small runtime changes could affect login or static assets. |
| 3. Add operations guardrails | Log retention and backup/migration evidence rules | Manual backup/restore may be skipped unless evidence is enforced socially. |
| 4. Build admin panel | Visible readiness checklist | UI might imply manual checks are automatically verified. |
| 5. Verify evidence | Final build/test/manual sign-off record | Free-tier recovery risk remains accepted, not eliminated. |

**Prerequisites:** Current `S-01` access-hardening work must remain true: the technical owner is not an interactive login path.  
**Estimated effort:** ~3-5 focused sessions across 5 phases, depending on test depth and manual Render validation.

## Open Risks & Assumptions

- Free Render Postgres can still lose data; manual `pg_dump` is a compensation, not provider-grade recovery.
- Restore smoke testing requires a non-production PostgreSQL target.
- Render CLI validation and live health checks require local credentials/network access.
- The remembered-session cookie remains a conscious MVP compromise because JavaScript cannot set `HttpOnly`.
- Future OCR/file upload work must not re-enable public `/files` without authenticated access.

## Success Criteria (Summary)

- Admin can see a readiness checklist that clearly marks Free Render as accepted risk and shows critical runtime checks.
- `/health/ready` verifies database connectivity and is used by Render.
- Evidence log records build/test, backup, restore smoke test, migration review, and human sign-off before real household data is considered ready.
