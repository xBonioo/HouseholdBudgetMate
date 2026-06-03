# Verify PIN-Gated Household Access - Plan Brief

> Full plan: `context/changes/verify-pin-gated-household-access/plan.md`

## What & Why

This change makes the existing PIN and household-profile feature satisfy the MVP access contract: administrators manage PIN-protected profiles, members unlock assigned budget data, and ordinary application use cannot expose the household budget without an established protected profile. The current implementation conflicts with that promise because its technical `default-user` administrator intentionally logs in without a PIN.

## Starting Point

The application already hashes PINs, assigns admin/member roles, scopes budget records by an owner user, blocks routed page content behind session initialization, and retains a remembered profile in a protected 30-day cookie. Today setup and tests also preserve an empty-PIN `default-user` administrator, while an unset data context defaults to that technical owner's budget.

## Desired End State

`default-user` remains an internal owner for existing shared-budget records but is hidden from sign-in and everyday profile management. A visible administrator profile protected by PIN controls household access; upgraded installations establish one before budget use resumes. Normal unauthenticated reads and writes fail closed, while the accepted trusted-browser cookie can still restore a previously unlocked eligible profile for up to 30 days.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Technical owner | Keep `default-user` hidden and non-interactive | Preserves existing shared-budget records without retaining a PIN bypass. |
| Administrator identity | First visible PIN-protected profile is administrator | Matches the PRD and the setup experience users actually interact with. |
| Existing installations | Require one-time access hardening before normal use | Closes the current exposure instead of allowing it indefinitely. |
| Session duration | Keep the protected 30-day trusted-profile restore | The user accepted remembered-browser convenience after initial PIN unlock. |
| Security boundary | UI gate plus fail-closed session/data scope | Removing only the dialog bypass would leave implicit technical-owner access paths. |
| Recovery | Local service/configuration recovery path | Avoids adding a daily-login bypass while preserving owner recovery. |
| Data transition | Keep financial rows owned by `default-user` | Avoids a high-risk multi-table ownership rewrite. |
| Verification | Automated service/session/scope tests plus manual UI flow | Matches existing test infrastructure while checking user-visible behavior. |

## Scope

**In scope:**

- Secure admin creation and one-time legacy access hardening.
- Removal of interactive PIN-less technical-owner login.
- Trusted-session eligibility checks and fail-closed budget context.
- Profile administration, local PIN recovery, and proof for `FR-008`/`FR-009`.

**Out of scope:**

- ASP.NET authentication/authorization redesign.
- Migrating existing financial rows away from `default-user`.
- Granular permissions beyond current roles and budget ownership.
- Removing the accepted 30-day trusted-browser restore.
- New automated UI testing infrastructure or broader `S-02` validation.

## Architecture / Approach

The plan separates internal ownership from user identity: `default-user` continues to own shared legacy data, while visible PIN-protected profiles are the only identities allowed to create sessions. Setup and upgrade gating ensure a secure administrator exists; session/data changes eliminate implicit access as the internal owner; administration and recovery operate only through secured visible profiles or an explicit local recovery flow.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Secure administrator and upgrade gate | New and upgraded installations establish a PIN-protected administrator before use | Hardening flow could lock out an existing household if incomplete |
| 2. PIN sessions and fail-closed scope | Technical login bypass is gone; no-session access cannot resolve owner data | Startup seeding must retain a controlled technical path |
| 3. Administration, recovery, evidence | Profile management and local reset complete the usable access workflow | Recovery must not become a normal-login bypass |

**Prerequisites:** The existing user-scoping migrations and current S-01 change identity; agreed decisions from this planning session.
**Estimated effort:** Approximately 3 focused implementation sessions across 3 phases.

## Open Risks & Assumptions

- The accepted 30-day cookie means PIN is not re-entered on a remembered browser; this is a deliberate convenience/security compromise.
- A concrete persistence mechanism for the access-hardening-required state may require a small schema or configuration change once implementation maps the cleanest existing pattern.
- Seeding and migration operations must use an explicit technical context after ordinary data access stops falling back to `default-user`.
- UI verification remains manual because the project does not currently contain component or end-to-end test infrastructure.

## Success Criteria (Summary)

- `default-user` cannot be used as an interactive PIN-less route to household data.
- A PIN-protected administrator can create/manage a member profile, and that member unlocks its assigned shared budget only through valid access.
- Existing insecure installations are hardened before regular use, forgotten admin PINs can be recovered locally, and normal no-session data access fails closed.
