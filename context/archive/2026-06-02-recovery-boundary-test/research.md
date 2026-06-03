---
date: 2026-06-02T18:59:40.1489114+02:00
researcher: Codex
git_commit: 8fb67dcabfbd5a80139d57c2ef046bb3b9fac584
branch: main
repository: HouseholdBudgetMate
topic: "recovery-boundary-test"
tags: [research, codebase, testing, access-recovery, session-restore, user-scope]
status: complete
last_updated: 2026-06-02
last_updated_by: Codex
---

# Research: recovery-boundary-test

**Date**: 2026-06-02T18:59:40.1489114+02:00
**Researcher**: Codex
**Git Commit**: 8fb67dcabfbd5a80139d57c2ef046bb3b9fac584
**Branch**: main
**Repository**: HouseholdBudgetMate

## Research Question

What should the next "recovery boundary test" cover for the phased test rollout, and where should it be implemented?

## Summary

The next test should target the boundary between local administrator recovery, remembered-session restore, and budget-owner scoping. The existing suite already tests those systems separately, but the high-risk gap is cross-component: after recovery resets or recreates an administrator, a stale trusted browser cookie must fail closed, the recovered administrator must sign in only with the new PIN, and the active session must scope budget access to the intended technical owner rather than accidentally becoming the technical owner.

This maps directly to test-plan phase 3, "Access restore boundaries", which covers risks #3 and #6: invalid or stale restore/recovery states must fail closed, and remembered sessions must not access the wrong household/profile. The cheapest high-signal layer is an integration/security service test using real persistence and real user/session/recovery services, plus a small middleware test if the implementation phase wants to pin recovery routing priority.

## Detailed Findings

### Rollout Oracle

- `context/foundation/test-plan.md:24` identifies risk #3: happy-path session restore or upgrade hardening can still leak budget data or block legitimate recovery after edge-case restore/recovery.
- `context/foundation/test-plan.md:27` identifies risk #6: logged-in or remembered sessions can access the wrong household/profile when ownership and technical-owner boundaries drift.
- `context/foundation/test-plan.md:35` says stale restore and upgrade states should fail closed without blocking valid recovery, and names the required oracle inputs: session restore states, hardening route, recovery path, trusted-browser lifetime, and technical-owner rule.
- `context/foundation/test-plan.md:38` says cross-profile or technical-owner access attempts must fail even with an otherwise valid session.
- `context/foundation/test-plan.md:48` marks phase 3 as "Access restore boundaries" for session restore, upgrade hardening, recovery, and ownership abuse cases.
- `context/foundation/test-plan.md:136` leaves the cookbook pattern for trusted-browser restore, upgrade hardening, recovery, and cross-profile denial as TBD for phase 3.

### Session Restore Boundary

- `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:25` stores the trusted profile in the `hbm_current_user_id` cookie.
- `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:36` starts restore by reading and unprotecting that cookie.
- `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:61` rejects a cookie with missing user id or security stamp.
- `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:70` restores only when both user id and `SessionSecurityStamp` match a current sign-in-eligible profile.
- `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:81` signs in through PIN validation and eligible sign-in profile lookup.
- `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:112` writes the current profile id and `SessionSecurityStamp` into the protected trusted cookie.
- `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:129` applies the restored/signed-in user through `CurrentUserContext.SetInteractiveUser`, so the context guard is part of the session contract.

Research implication: the test should not assert only that `AccessRecoveryService` changes rows. It should prove an old cookie produced before recovery no longer restores after recovery changes the administrator's PIN/security stamp, then prove the recovered admin can create a fresh valid session.

### Recovery Boundary

- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:26` treats `IsLocalAccessRecoveryEnabled` as the durable "recovery required" flag.
- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:60` serializes recovery with `RecoveryLock`.
- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:68` requires a valid local access recovery grant before changing administrator credentials.
- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:75` ensures the technical owner exists as `User.DefaultUserId`.
- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:89` clears the technical owner's password hash.
- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:90` ensures the technical owner is not an administrator.
- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:91` keeps the technical owner linked to `User.DefaultUserId`.
- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:95` finds a visible administrator by username while excluding `User.DefaultUserId`.
- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:110` writes the recovered PIN hash.
- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:111` makes the recovered visible profile an administrator.
- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:112` links the recovered visible admin to `User.DefaultUserId` as budget owner.

Research implication: the oracle is "visible administrator recovered, technical owner remains non-interactive, shared budget owner preserved." A regression where recovery promotes `default-user`, keeps a stale cookie valid, or assigns the wrong budget owner should fail.

### Technical Owner And Budget Scope

- `src/HouseholdBudgetMate.Domain/Infrastructure/CurrentUserContext.cs:11` provides an explicit technical-owner context for system/bootstrap work.
- `src/HouseholdBudgetMate.Domain/Infrastructure/CurrentUserContext.cs:39` rejects interactive users with an empty id, the technical owner id, or an empty budget owner.
- `src/HouseholdBudgetMate.Domain/Infrastructure/CurrentUserContext.cs:53` clears interactive state by removing `BudgetOwnerUserId`.
- `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:13` defines a no-access sentinel for missing/invalid budget scope.
- `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:16` returns the current budget owner only when the context has an authorized scope.
- `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:127` throws on writes when a user-scoped entity has no authorized budget owner.
- `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:138` decides whether the current context can read/write user-scoped data.
- `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:204` through `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:218` apply query filters to user-scoped financial entities.

Research implication: a good boundary test should include at least one persisted user-scoped budget row owned by `default-user` and prove it is visible only after the recovered visible admin establishes an interactive scope with `BudgetOwnerUserId == User.DefaultUserId`. It should also prove a stale or cleared session does not silently read that row.

### Sign-In Eligibility

- `src/HouseholdBudgetMate.Application/Services/UserService.cs:33` exposes the list of profiles eligible for sign-in.
- `src/HouseholdBudgetMate.Application/Services/UserService.cs:37` filters sign-in users to `IsInteractive && HasPin`.
- `src/HouseholdBudgetMate.Application/Services/UserService.cs:187` validates PINs.
- `src/HouseholdBudgetMate.Application/Services/UserService.cs:204` returns false for technical-owner PIN validation.
- `src/HouseholdBudgetMate.Application/Services/UserService.cs:298` maps persisted users to DTOs.
- `src/HouseholdBudgetMate.Application/Services/UserService.cs:308` marks only non-technical-owner users as interactive.
- `src/HouseholdBudgetMate.Application/Services/UserService.cs:309` derives `SessionSecurityStamp` from the current persisted user state.
- `src/HouseholdBudgetMate.Application/Services/UserService.cs:310` identifies the technical owner as the default admin record.
- `src/HouseholdBudgetMate.Application/Services/UserService.cs:311` prevents the technical owner from being treated as an interactive admin.

Research implication: the integration test should use real `UserService` instead of a mock for the critical stale-cookie assertion. Existing mocked tests can prove local session behavior, but the cross-component risk depends on the real stamp changing when recovery updates the PIN hash.

### Recovery Routing

- `src/HouseholdBudgetMate.Web/Middleware/AccessHardeningRedirectMiddleware.cs:39` checks recovery before access hardening.
- `src/HouseholdBudgetMate.Web/Middleware/AccessHardeningRedirectMiddleware.cs:41` redirects local recovery-required requests to `/access-recovery` with an access-recovery grant.
- `src/HouseholdBudgetMate.Web/Middleware/AccessHardeningRedirectMiddleware.cs:47` only reaches `/access-setup` when recovery is not required and access hardening is required.
- `src/HouseholdBudgetMate.Web/Middleware/AccessHardeningRedirectMiddleware.cs:63` excludes setup/recovery paths from ordinary redirects.
- `src/HouseholdBudgetMate.Web/Middleware/AccessHardeningRedirectMiddleware.cs:98` returns 403 when a required local flow is requested from a non-loopback client.

Research implication: if phase 3 includes a middleware slice, pin the recovery-priority rule with a focused middleware test. This is cheaper and clearer than driving the whole UI.

## Existing Test Coverage

- `src/HouseholdBudgetMate.Tests/Tests/Services/UserSessionServiceTests.cs:17` covers successful PIN sign-in and trusted cookie creation.
- `src/HouseholdBudgetMate.Tests/Tests/Services/UserSessionServiceTests.cs:38` covers technical-owner cookie rejection.
- `src/HouseholdBudgetMate.Tests/Tests/Services/UserSessionServiceTests.cs:78` covers eligible trusted profile restore.
- `src/HouseholdBudgetMate.Tests/Tests/Services/UserSessionServiceTests.cs:134` covers stale cookie deletion after PIN/security-stamp changes, but with mocked `IUserService`.
- `src/HouseholdBudgetMate.Tests/Tests/Services/AccessRecoveryServiceTests.cs:15` covers disabled recovery rejection.
- `src/HouseholdBudgetMate.Tests/Tests/Services/AccessRecoveryServiceTests.cs:33` covers visible admin reset and recovery mode disablement.
- `src/HouseholdBudgetMate.Tests/Tests/Services/AccessRecoveryServiceTests.cs:75` covers creating a visible admin without making the technical owner interactive.
- `src/HouseholdBudgetMate.Tests/Tests/Services/AccessRecoveryServiceTests.cs:110` covers missing local grant rejection.
- `src/HouseholdBudgetMate.Tests/Tests/Services/AccessRecoveryServiceTests.cs:124` covers keeping recovery mode enabled when DB save fails.
- `src/HouseholdBudgetMate.Tests/Tests/Services/UserScopingTests.cs:276` covers null budget-owner fail-closed behavior.
- `src/HouseholdBudgetMate.Tests/Tests/Services/UserScopingTests.cs:414` covers missing user context failing closed for technical-owner-owned budget data.
- `src/HouseholdBudgetMate.Tests/Tests/Services/UserServiceAuthorizationTests.cs:685` covers technical-owner PIN validation returning false.
- `src/HouseholdBudgetMate.Tests/Tests/Services/UserServiceAuthorizationTests.cs:773` covers excluding the technical owner and pinless profiles from sign-in users.

Gap: there is no single test that runs recovery and then validates the remembered-session boundary with real persisted user records, real sign-in eligibility, real session stamp generation, and budget-owner scope.

## Recommended Test Shape

1. Add a focused integration/security test under `src/HouseholdBudgetMate.Tests/Tests/Services/`, likely in a new `RecoveryBoundaryTests.cs` or as an additional test near `AccessRecoveryServiceTests`.
2. Seed:
   - technical owner `default-user`;
   - visible admin with old PIN and `BudgetOwnerUserId = User.DefaultUserId`;
   - at least one user-scoped budget row owned by `default-user`;
   - runtime config with local recovery enabled.
3. Sign in through real `UserSessionService` and real `UserService` using the old PIN to produce a trusted cookie.
4. Run real `AccessRecoveryService.ResetAdministratorAccessAsync` with a valid local recovery grant and a new PIN.
5. Attempt restore from the old trusted cookie through a fresh `UserSessionService`; assert restore is false, cookie is cleared, and budget rows are not visible without a valid interactive scope.
6. Sign in with the new PIN; assert success, `CurrentUserContext.UserId` is the visible admin, `CurrentUserContext.BudgetOwnerUserId` is `User.DefaultUserId`, and the seeded `default-user` budget row is visible.
7. Assert the technical owner remains excluded from `GetSignInUsersAsync` and is not an interactive admin.

Optional companion test: add a middleware test proving recovery-required requests are routed to `/access-recovery` before access-hardening routes when both readiness conditions could be true.

## Code References

- `context/foundation/test-plan.md:24` - risk #3 for stale restore/recovery leakage.
- `context/foundation/test-plan.md:27` - risk #6 for wrong household/profile access.
- `context/foundation/test-plan.md:48` - phase 3 rollout item for access restore boundaries.
- `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:36` - trusted-cookie restore entry point.
- `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:70` - restore requires matching current session stamp.
- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:68` - recovery requires a local grant.
- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:110` - recovery updates the visible admin PIN hash.
- `src/HouseholdBudgetMate.Application/Services/UserService.cs:309` - session security stamp is derived from persisted user state.
- `src/HouseholdBudgetMate.Domain/Infrastructure/CurrentUserContext.cs:39` - technical owner cannot become an interactive user.
- `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:204` - financial entity query filters use the current budget-owner scope.
- `src/HouseholdBudgetMate.Tests/Tests/Services/UserSessionServiceTests.cs:134` - existing stale-cookie test, currently mocked below the cross-component boundary.
- `src/HouseholdBudgetMate.Tests/Tests/Services/AccessRecoveryServiceTests.cs:33` - existing recovery admin reset test.

## Architecture Insights

The access model intentionally separates identity from ownership. `default-user` remains the internal owner for existing shared household data, while visible PIN-protected profiles are the only interactive identities. This means "can restore session" and "can read budget rows" are not independent checks: a restored session must carry the visible profile id and the correct budget owner id.

The highest-risk regression class is not a broken happy path. It is a stale or privileged path surviving a credential reset: the browser has a validly protected cookie from before recovery, but the recovered administrator now has a new PIN hash and therefore a new security stamp. That cookie must not rehydrate an old session.

The current design gives a clean cheap test seam: real EF in-memory/SQLite-style persistence, real `UserService`, real `AccessRecoveryService`, and real `UserSessionService` with a small fake `IJSRuntime` cookie store. That keeps the test behavioral without launching the UI.

## Historical Context

- `context/changes/verify-pin-gated-household-access/plan.md:13` records the accepted 30-day protected cookie behavior after successful unlock.
- `context/changes/verify-pin-gated-household-access/plan.md:19` states `default-user` remains the internal budget owner but is not listed for sign-in or interactive administration.
- `context/changes/verify-pin-gated-household-access/plan.md:21` states that without validated PIN sign-in or trusted restore, normal budget reads and writes fail closed instead of falling back to `default-user`.
- `context/changes/verify-pin-gated-household-access/plan.md:60` states that removing a user, losing configured PIN, or hardening/recovery changes must invalidate or block trusted restore.
- `context/changes/verify-pin-gated-household-access/plan.md:207` defines local recovery as a way to establish/reset a visible administrator linked to the technical owner without becoming a login-screen bypass.
- `context/changes/verify-pin-gated-household-access/plan.md:215` requires tests for restore-cookie eligibility, local recovery of an inaccessible admin, and denial of budget scope without an eligible session.
- `context/changes/verify-pin-gated-household-access/plan.md:255` and `context/changes/verify-pin-gated-household-access/plan.md:256` list manual acceptance checks for trusted restore and local recovery.

## Related Research

- `context/changes/testing-cross-screen-monthly-consistency/research.md` - earlier test rollout research for cross-screen consistency.
- `context/changes/real-data-readiness-gates/research.md` - recent test rollout research pattern for readiness/security gates.
- `context/changes/verify-pin-gated-household-access/plan.md` - historical oracle for S-01 access hardening, remembered sessions, recovery, and technical owner separation.

## Open Questions

- Should the implementation phase include only the cross-component recovery/session/scope test, or also the smaller middleware recovery-priority test? The research recommendation is to include the integration/security boundary first and add middleware only if phase budget allows.
- Should the test use the existing EF provider helper used by service tests or a stricter relational provider if available? The behavior under test is mostly service state and query filtering, so the existing test database pattern should be sufficient unless the plan discovers relational-only ownership behavior.
