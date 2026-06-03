# Recovery Boundary Test Implementation Plan

## Overview

Add Phase 3 rollout tests for the access restore boundary: local administrator recovery, stale trusted-browser restore, and technical-owner budget scope. The goal is not to add a new recovery feature; it is to prove the existing recovery and session model fails closed when an administrator PIN is reset and still allows the recovered visible administrator back into the intended household budget.

## Current State Analysis

The access model intentionally separates interactive identity from shared budget ownership. `default-user` remains the internal owner for existing shared household data, while visible PIN-protected users are the only identities allowed to sign in. `UserSessionService` stores a protected cookie containing the user id and session security stamp, `UserService` derives sign-in eligibility and stamps from persisted user state, and `CurrentUserContext` rejects `default-user` as an interactive user.

Existing tests cover these pieces separately. Session tests cover sign-in, trusted restore, technical-owner rejection, sign-out, and stale cookie deletion with a mocked `IUserService`. Recovery tests cover disabled recovery, visible admin reset, creating a visible admin, missing grant rejection, and failed database save behavior. Scoping tests cover fail-closed budget filters. The remaining rollout gap is the cross-component boundary after recovery changes persisted credentials.

## Desired End State

The test suite proves that a trusted cookie minted before local recovery cannot restore after recovery resets the visible administrator PIN. It also proves the recovered admin can sign in with the new PIN, receives `BudgetOwnerUserId = default-user`, can see budget rows owned by that technical owner, and does not make `default-user` interactive.

The middleware test suite also proves recovery-required routing wins over ordinary access-hardening setup routing. The rollout cookbook records the recovery-boundary pattern for future agents.

### Key Discoveries:

- `context/foundation/test-plan.md:48` identifies Phase 3 as "Access restore boundaries" for restore, hardening, recovery, and ownership abuse cases.
- `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:70` restores only when the cookie user id and `SessionSecurityStamp` match a current sign-in-eligible profile.
- `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:110` updates the recovered visible administrator PIN hash.
- `src/HouseholdBudgetMate.Application/Services/UserService.cs:309` derives `SessionSecurityStamp` from persisted user state, so a recovery PIN reset should invalidate old trusted cookies.
- `src/HouseholdBudgetMate.Domain/Infrastructure/CurrentUserContext.cs:39` rejects the technical owner as an interactive user.
- `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:204` applies financial query filters using the current budget-owner scope.
- `src/HouseholdBudgetMate.Web/Middleware/AccessHardeningRedirectMiddleware.cs:39` checks recovery before access hardening.

## Planning Decisions

| Decision | Choice | Rationale |
| --- | --- | --- |
| Boundary coverage shape | Several focused cross-component tests | Keeps failure diagnosis clearer than one long scenario while still covering recovery/session/scope integration. |
| Middleware coverage | Add recovery-priority test | Pins the route-order part of the rollout risk without launching UI. |
| Budget data fixture | Seed a `default-user` owned budget row | Proves risk #6 at the data-access layer, not only the login/session layer. |
| Phase structure | Two phases | Phase 1 ships the high-signal service/security tests; Phase 2 closes routing and cookbook documentation. |
| Production changes | Test-only unless a regression is found | The current feature exists; this rollout phase is about executable protection. |

## What We're NOT Doing

- Not adding a new recovery workflow, UI, route, database field, or production approval flag.
- Not removing the accepted 30-day trusted-browser behavior for valid visible profiles.
- Not migrating financial rows away from `default-user`.
- Not turning `default-user` into an interactive user.
- Not writing browser/e2e tests for this phase.
- Not mocking the critical stale-cookie/recovery boundary where real `UserService` and persisted user state are required.

## Implementation Approach

Add a new focused integration/security test file under `src/HouseholdBudgetMate.Tests/Tests/Services/`, or extend the existing service test area if the local helper patterns make that cleaner. Use real `ApplicationDbContext`, real `UserService`, real `AccessRecoveryService`, real `UserSessionService`, Data Protection, and a small fake `IJSRuntime` cookie store. Keep the tests small by splitting the scenario into separate assertions: stale trusted cookie invalidation, recovered admin sign-in/scope, and technical-owner exclusion.

Use existing middleware test patterns to add a focused routing test for recovery priority. Update `context/foundation/test-plan.md` only after the code tests pass so the cookbook reflects the actually shipped pattern.

## Phase 1: Recovery Session And Scope Boundary

### Overview

Add integration/security tests that exercise recovery, trusted-cookie invalidation, and budget-owner scope with real persisted state.

### Changes Required:

#### 1. Recovery Boundary Test Fixture

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/RecoveryBoundaryTests.cs`

**Intent**: Create a small, reusable fixture for the recovery-boundary tests without hiding the oracle inside helper logic.

**Contract**: Seed a technical owner `User.DefaultUserId`, a visible admin with old PIN and `BudgetOwnerUserId = User.DefaultUserId`, local recovery enabled state, and at least one user-scoped budget row owned by `User.DefaultUserId`. Use a fake cookie `IJSRuntime` compatible with `UserSessionService` and real Data Protection.

#### 2. Stale Trusted Cookie After Recovery Test

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/RecoveryBoundaryTests.cs`

**Intent**: Prove a trusted cookie minted before recovery cannot restore after recovery resets the admin PIN.

**Contract**: Sign in through real `UserSessionService` with the old PIN, capture the trusted cookie, run real `AccessRecoveryService.ResetAdministratorAccessAsync` with a valid local recovery grant and new PIN, then attempt restore with a fresh session context using the old cookie. Assert restore returns false, the cookie is cleared, and unauthenticated budget scope cannot read the seeded `default-user` budget row.

#### 3. Recovered Admin Scope Test

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/RecoveryBoundaryTests.cs`

**Intent**: Prove recovery restores legitimate access through the visible admin rather than through the technical owner.

**Contract**: After recovery, sign in with the new PIN. Assert sign-in succeeds, `CurrentUserContext.UserId` is the visible admin id, `CurrentUserContext.BudgetOwnerUserId` is `User.DefaultUserId`, and the seeded `default-user` budget row is visible through the recovered interactive scope.

#### 4. Technical Owner Remains Non-Interactive Test

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/RecoveryBoundaryTests.cs`

**Intent**: Guard against recovery accidentally promoting the technical owner into a login profile.

**Contract**: After recovery, call real `UserService.GetSignInUsersAsync` and assert the technical owner is absent, the visible recovered admin is present, and `ValidatePinAsync(User.DefaultUserId, ...)` remains false.

### Success Criteria:

#### Automated Verification:

- Recovery boundary tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RecoveryBoundaryTests"`
- Existing session/recovery/scoping tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~UserSessionServiceTests|FullyQualifiedName~AccessRecoveryServiceTests|FullyQualifiedName~UserScopingTests|FullyQualifiedName~UserServiceAuthorizationTests"`

#### Manual Verification:

- Review the tests and confirm expected values come from the access contract, not from recomputing implementation internals.
- Confirm the test uses real `UserService`, `AccessRecoveryService`, and persisted users for the stale-cookie/recovery boundary.
- Confirm no production code changed unless a failing test exposed an actual bug.

**Implementation Note**: If the old trusted cookie still restores after recovery, stop and treat it as a product/security bug. Do not weaken the oracle to match the current behavior.

---

## Phase 2: Recovery Routing And Cookbook Closure

### Overview

Pin recovery route priority and record the access-restore boundary pattern in the test rollout cookbook.

### Changes Required:

#### 1. Recovery Priority Middleware Test

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/AccessHardeningRedirectMiddlewareTests.cs`

**Intent**: Prove recovery-required routing takes priority over normal access-hardening setup routing.

**Contract**: Add a focused middleware test where recovery is required and access hardening would otherwise be required. For a local GET request to a normal app path, assert the response redirects to `/access-recovery` and not `/access-setup`, with a recovery grant purpose.

#### 2. Recovery Route Remote Denial Check

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/AccessHardeningRedirectMiddlewareTests.cs`

**Intent**: Keep recovery from becoming remotely reachable when the recovery-required state is active.

**Contract**: Add or strengthen a middleware assertion that a non-loopback request during recovery-required state receives 403 rather than a recovery grant or redirect.

#### 3. Rollout Cookbook Entry

**File**: `context/foundation/test-plan.md`

**Intent**: Document the pattern for testing recovery boundaries without falling into happy-path-only or mirror-test coverage.

**Contract**: Fill the Phase 3 cookbook placeholder for trusted-browser restore, recovery, upgrade hardening, and cross-profile denial. Include the split between service integration tests, middleware routing tests, budget-scope assertions, and manual review. Note that valid trusted-browser restore remains accepted only for current visible profiles.

#### 4. Final Verification

**File**: `src/HouseholdBudgetMate.Tests/Tests/Services/RecoveryBoundaryTests.cs`

**Intent**: Ensure the new tests coexist with the full access and setup surface.

**Contract**: Run targeted access-restore tests, the full release test suite, release build, and git whitespace check.

### Success Criteria:

#### Automated Verification:

- Middleware recovery routing tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~AccessHardeningRedirectMiddlewareTests"`
- Targeted access restore tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RecoveryBoundaryTests|FullyQualifiedName~UserSessionServiceTests|FullyQualifiedName~AccessRecoveryServiceTests|FullyQualifiedName~UserScopingTests|FullyQualifiedName~AccessHardeningRedirectMiddlewareTests"`
- Full release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release`
- Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release`
- Git whitespace check passes: `git diff --check -- .`

#### Manual Verification:

- Review `context/foundation/test-plan.md` and confirm the recovery-boundary cookbook explains stale trusted-cookie invalidation, recovered admin scope, technical-owner exclusion, and recovery route priority.
- Confirm Markdown/context artifacts can remain uncommitted if the user requests code-only commits.
- Confirm no browser/e2e or live infrastructure requirement was introduced.

**Implementation Note**: Keep cookbook wording compact and tied to the tests that actually ship in this change.

---

## Testing Strategy

### Unit Tests:

- No pure unit tests are expected.
- The critical signal comes from integration/security tests using real services and persisted user state.

### Integration / Security Tests:

- Add `RecoveryBoundaryTests` for recovery/session/scope behavior.
- Use real `UserService`, `UserSessionService`, `AccessRecoveryService`, `ApplicationDbContext`, Data Protection, and local recovery grant handling.
- Seed `default-user` owned budget data to prove fail-closed behavior and recovered scope visibility.

### Middleware Tests:

- Extend `AccessHardeningRedirectMiddlewareTests` with recovery-priority and remote-denial coverage.
- Keep the middleware assertions route-level and grant-purpose-level; do not render pages.

### Manual Testing Steps:

1. Review the new boundary tests and verify the assertions are oracle-driven: old cookie fails, new PIN works, visible admin owns the interactive session, `default-user` remains non-interactive.
2. Review the middleware test and confirm recovery goes to `/access-recovery` before `/access-setup`.
3. Review the cookbook update and confirm future agents can reproduce the pattern without adding broad e2e tests.

## Performance Considerations

The tests should use local in-process services and local test persistence only. Avoid live network calls, browser automation, or external database tools. Splitting the main boundary into a few tests may add setup repetition, but the suite should remain cheap enough for targeted and full release runs.

## Migration Notes

No production migration is planned. The tests must preserve the existing decision that financial rows can remain owned by `default-user` while visible profiles are the only interactive identities.

## References

- Related research: `context/changes/recovery-boundary-test/research.md`
- Rollout plan: `context/foundation/test-plan.md`
- Existing session tests: `src/HouseholdBudgetMate.Tests/Tests/Services/UserSessionServiceTests.cs:17`
- Existing recovery tests: `src/HouseholdBudgetMate.Tests/Tests/Services/AccessRecoveryServiceTests.cs:33`
- Existing scoping tests: `src/HouseholdBudgetMate.Tests/Tests/Services/UserScopingTests.cs:414`
- Existing middleware tests: `src/HouseholdBudgetMate.Tests/Tests/Services/AccessHardeningRedirectMiddlewareTests.cs`
- Session restore service: `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:36`
- Recovery service: `src/HouseholdBudgetMate.Web/Setup/AccessRecoveryService.cs:68`
- User sign-in service: `src/HouseholdBudgetMate.Application/Services/UserService.cs:33`
- Current user context: `src/HouseholdBudgetMate.Domain/Infrastructure/CurrentUserContext.cs:39`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` - <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Recovery Session And Scope Boundary

#### Automated

- [x] 1.1 Recovery boundary tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RecoveryBoundaryTests"` â€” f21d050
- [x] 1.2 Existing session/recovery/scoping tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~UserSessionServiceTests|FullyQualifiedName~AccessRecoveryServiceTests|FullyQualifiedName~UserScopingTests|FullyQualifiedName~UserServiceAuthorizationTests"` â€” f21d050

#### Manual

- [x] 1.3 Review the tests and confirm expected values come from the access contract, not from recomputing implementation internals â€” f21d050
- [x] 1.4 Confirm the stale-cookie/recovery boundary uses real services and persisted users â€” f21d050
- [x] 1.5 Confirm no production code changed unless a failing test exposed an actual bug â€” f21d050

### Phase 2: Recovery Routing And Cookbook Closure

#### Automated

- [x] 2.1 Middleware recovery routing tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~AccessHardeningRedirectMiddlewareTests"` â€” e26c346
- [x] 2.2 Targeted access restore tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~RecoveryBoundaryTests|FullyQualifiedName~UserSessionServiceTests|FullyQualifiedName~AccessRecoveryServiceTests|FullyQualifiedName~UserScopingTests|FullyQualifiedName~AccessHardeningRedirectMiddlewareTests"` â€” e26c346
- [x] 2.3 Full release test suite passes: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release` â€” e26c346
- [x] 2.4 Release build passes: `dotnet build HouseholdBudgetMate.slnx -c Release` â€” e26c346
- [x] 2.5 Git whitespace check passes: `git diff --check -- .` â€” e26c346

#### Manual

- [x] 2.6 Review `context/foundation/test-plan.md` and confirm it documents stale trusted-cookie invalidation, recovered admin scope, technical-owner exclusion, and recovery route priority â€” e26c346
- [x] 2.7 Confirm Markdown/context artifacts can remain uncommitted if the user requests code-only commits â€” e26c346
- [x] 2.8 Confirm no browser/e2e or live infrastructure requirement was introduced â€” e26c346
