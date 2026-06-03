# Verify PIN-Gated Household Access Implementation Plan

## Overview

Bring the existing household profile and PIN flow into compliance with `S-01`, `FR-008`, and `FR-009`: an administrator can manage PIN-protected household profiles, a member can unlock the assigned budget, and ordinary application use cannot reveal or mutate budget data before a protected profile has been established. Preserve the existing shared-budget data model by retaining `default-user` as an internal budget owner, while removing it from interactive login and administrator access.

## Current State Analysis

The application already has a substantial access-control implementation: users have PIN hashes and admin roles, budget entities are scoped through `BudgetOwnerUserId`, the login dialog selects a profile, and `MainLayout` withholds its page body until a session exists. However, the implementation contradicts the PRD's PIN boundary in several material places:

- The seeded and setup-maintained `default-user` is an administrator whose hash is blank and whose profile is treated as intentionally PIN-less.
- The login dialog explicitly permits entry as the default administrator without a PIN, while tests record this as accepted behavior.
- A protected client cookie restores a selected profile for 30 days without re-entering a PIN; the user has accepted retaining this trusted-session behavior after the first successful unlock.
- The data context defaults to `default-user` when no user context is supplied, and the scoped `CurrentUserContext` itself starts at that user, so missing session state can select technical-owner data unless every call is correctly gated in UI.
- The runtime setup redirect only determines whether database configuration exists; it does not detect an existing installation that lacks an interactive administrator protected by a PIN.

## Desired End State

`default-user` remains the internal owner for existing shared household budget records, but it is not listed as a sign-in profile, is not granted interactive administration through its technical identity, and cannot be used to bypass PIN unlock. New setup creates or promotes a PIN-protected interactive administrator linked to the technical budget owner. Existing installations with no valid PIN-protected administrator are routed through a one-time access-hardening step before budget screens are available.

Once a protected interactive profile has successfully signed in, the existing 30-day protected cookie may restore that profile on the same browser as a consciously accepted convenience tradeoff. Without either a validated PIN sign-in or a valid trusted-session restore, normal budget reads and writes fail closed instead of falling back to `default-user`. An explicit system-owned bootstrap/seeding path may continue to operate against the technical owner outside user-facing interactions.

Completion is proven by automated tests covering user/PIN authorization, session restoration and rejection, access-hardening readiness, and fail-closed scoping behavior, followed by manual verification of setup, login, profile switching, administration, trusted restore, and local recovery.

### Key Discoveries:

- [UserService.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Application/Services/UserService.cs:123) blocks PIN assignment to `default-user`, and its PIN validation treats that profile as valid with an empty PIN.
- [UserLoginDialog.razor](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Components/Dialogs/UserLoginDialog.razor:27) explicitly tells users that the default administrator does not require a PIN.
- [SetupConfigurationService.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Setup/SetupConfigurationService.cs:95) ensures a blank-PIN default administrator and creates a separate first user without admin privileges.
- [UserSessionService.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:35) restores any stored profile identifier from a data-protected, 30-day browser cookie without a subsequent PIN challenge.
- [MainLayout.razor](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Web/Components/Layout/MainLayout.razor:28) hides routed budget content until session initialization completes, but [ApplicationDbContext.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:11) and [CurrentUserContext.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Domain/Infrastructure/CurrentUserContext.cs:5) still select `default-user` by default.
- [UserServiceAuthorizationTests.cs](F:/Kamil/.Net/_projects/HouseholdBudgetMate/src/HouseholdBudgetMate.Tests/Tests/Services/UserServiceAuthorizationTests.cs:411) establishes test coverage for roles and PIN validation but currently asserts the PIN-less administrator exception; no automated session or Razor component tests were found.
- The current targeted access test baseline is green: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~UserServiceAuthorizationTests|FullyQualifiedName~UserScopingTests"` passes `34/34` tests before implementation.

## What We're NOT Doing

- Replacing the current profile/PIN model with ASP.NET authentication and authorization policies in this slice.
- Removing the internal `default-user` ownership model or rewriting existing household budget records onto a new administrator account.
- Adding per-module or per-record granular permissions beyond the existing admin/member and shared/separate budget behavior.
- Removing trusted-device session restore; the selected contract keeps the current 30-day protected cookie for successfully unlocked visible profiles.
- Introducing a new browser/component automation framework solely for this slice.
- Completing the monthly financial-loop validation in `S-02` or infrastructure readiness in `F-02`.

## Implementation Approach

Separate technical data ownership from interactive identity. Keep `default-user` only as the budget owner required by existing foreign keys and shared-budget records; introduce application-level rules that only visible, PIN-configured user profiles may create an interactive session and that at least one of those profiles carries administrator privileges. Update setup and upgrade readiness to guarantee that rule before ordinary routing reaches budget content. Then make the user/session and data-scoping boundaries fail closed, with an explicit bootstrap context used only for initial data ownership and controlled seeding.

## Critical Implementation Details

### State Sequencing

The one-time hardening gate must run before any normal layout session restore or budget loading. If an existing installation contains only the technical `default-user`, redirecting the user to ordinary login would create an unsatisfiable flow once technical login is removed.

### Security Boundary

Eliminating `default-user` from the dialog is insufficient: the current `CurrentUserContext` and `ApplicationDbContext` fallback can still resolve its budget data in service paths executed without an authenticated profile. Interactive requests must have an established visible user identity; technical-owner access should require an explicit bootstrap/system context rather than an implicit blank context.

### User Experience Spec

The accepted 30-day cookie represents a remembered trusted browser after a successful PIN entry. It must restore only profiles eligible for interactive login; removal of a user, loss of its configured PIN, or demotion/removal of the last secure administrator during hardening must invalidate or block that restore rather than expose the budget.

## Phase 1: Establish a Secure Administrator and Upgrade Gate

### Overview

Define the technical-owner versus interactive-profile contract, make new setup produce a usable PIN-protected administrator, and require legacy installations to establish that administrator before normal application access.

### Changes Required:

#### 1. Profile Contract and Setup Result

**File**: `src/HouseholdBudgetMate.Abstractions/Contracts/Users/Dto/UserDto.cs`

**Intent**: Give the web/session layer a reliable indication of whether a database user is eligible for interactive sign-in rather than inferring this only from its identifier and PIN presence.

**Contract**: Extend the user projection contract with the minimum status needed to distinguish internal ownership identities from visible sign-in profiles, while preserving admin and budget-owner information used in existing administration flows.

#### 2. User Profile Policy

**File**: `src/HouseholdBudgetMate.Application/Services/UserService.cs`

**Intent**: Stop treating technical ownership as an interactive administrator and express the application rules for secure admin availability and sign-in visibility.

**Contract**: `default-user` remains queryable where internal ownership management requires it, but it is not an eligible sign-in profile and empty-PIN validation is no longer a successful interactive login path. Add service-level operations or filtered reads required to determine whether at least one visible PIN-configured administrator exists and to manage visible profiles without violating technical-owner invariants.

#### 3. Initial Configuration Creates a Secure Admin

**File**: `src/HouseholdBudgetMate.Web/Setup/SetupConfigurationService.cs`

**Intent**: Align clean installation setup with the chosen identity model: the first named profile entered by the user must be able to administer profiles and share the existing technical owner's budget.

**Contract**: Continue creating/retaining `default-user` as the internal shared-budget owner with no interactive credentials; create or reconcile the configured application profile with a hashed 4-8 digit PIN, `IsAdmin = true`, and `BudgetOwnerUserId = default-user` in shared-budget mode. Setup must not reset a valid interactive administrator into a non-admin profile.

#### 4. Access-Hardening Readiness and Upgrade Routing

**Files**: `src/HouseholdBudgetMate.Web/Middleware/SetupRedirectMiddleware.cs`, `src/HouseholdBudgetMate.Web/Setup/RuntimeConfigurationState.cs`, new or existing setup/hardening page and supporting service under `src/HouseholdBudgetMate.Web/Setup/` and `src/HouseholdBudgetMate.Web/Components/Pages/`

**Intent**: Ensure an upgraded installation cannot reach regular login or budget pages until it has a PIN-protected interactive administrator.

**Contract**: Extend routing readiness beyond database configuration to an access-hardening state backed by user records: when no visible PIN-configured administrator exists, allow only setup/static/error paths plus a one-time local hardening flow that creates or promotes an admin profile linked to `default-user`; block ordinary application routes until it succeeds.

### Success Criteria:

#### Automated Verification:

- The solution compiles after identity/setup contract changes: `dotnet build HouseholdBudgetMate.slnx`
- User authorization tests prove technical-owner login is not a supported PIN path and a PIN-protected visible administrator can perform admin actions: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~UserServiceAuthorizationTests"`
- Setup/hardening service tests prove a fresh installation and an installation with only `default-user` result in a visible PIN-protected administrator before ordinary access is ready: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~Setup|FullyQualifiedName~Access"`

#### Manual Verification:

- Run first-time setup and confirm the entered household profile signs in with its PIN, can open administration, and sees data owned through the internal shared-budget owner without any selectable technical profile.
- Start from an existing database containing only the PIN-less technical administrator and confirm ordinary pages are unavailable until the one-time access-hardening flow creates or promotes a PIN-protected admin.

**Implementation Note**: After completing this phase and all automated verification passes, pause for manual confirmation that fresh setup and legacy hardening both leave an administrator able to access the household safely before changing the normal session/data boundary.

---

## Phase 2: Enforce PIN-Gated Sessions and Fail-Closed Budget Scope

### Overview

Remove interactive bypasses, retain trusted-session convenience only for eligible profiles, and ensure absent session state cannot silently select technical-owner data in normal application operation.

### Changes Required:

#### 1. Visible Profile Sign-In Experience

**File**: `src/HouseholdBudgetMate.Web/Components/Dialogs/UserLoginDialog.razor`

**Intent**: Present only profiles that can satisfy the PIN-unlock contract and remove the current administrator-without-PIN affordance.

**Contract**: The sign-in list excludes the technical owner, requires a valid PIN-capable profile selection, and never renders the "default administrator does not require PIN" path. An absence of eligible profiles routes into access hardening instead of allowing entry.

#### 2. Session Establishment and Trusted Restore

**File**: `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs`

**Intent**: Make a validated PIN or valid trusted restore the only routes into an interactive user session.

**Contract**: `SignInAsync` rejects technical or PIN-less profiles and persists the protected 30-day cookie only after successful visible-profile PIN validation. `TryRestoreFromCookieAsync` may restore a still-eligible visible profile without re-entering PIN, but must remove stale or newly ineligible cookies and may not restore the internal owner.

#### 3. Session Initialization and Hardening Transition

**File**: `src/HouseholdBudgetMate.Web/Components/Layout/MainLayout.razor`

**Intent**: Preserve the existing content-covering behavior while handling the hardening-required state and invalid trusted restores without ever rendering a budget view early.

**Contract**: The layout keeps `@Body` hidden until either a valid eligible session exists or navigation has been handed off to the hardening flow. Switching users clears current budget state and requires a valid profile selection or a valid remembered session.

#### 4. Explicit Interactive Scope

**Files**: `src/HouseholdBudgetMate.Domain/Infrastructure/CurrentUserContext.cs`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs`

**Intent**: Replace accidental technical-owner selection with a context model that distinguishes normal interactive access from intentional bootstrap/system access.

**Contract**: An unset interactive user must not resolve `CurrentBudgetOwnerUserId` to `default-user` for ordinary service/query execution. Provide an explicit technical-owner/bootstrap mode or factory usage for operations intentionally creating or maintaining internal shared-budget data; interactive queries and new entity stamping fail before a signed-in user has selected a budget owner.

#### 5. Bootstrap and Audit Compatibility

**Files**: `src/HouseholdBudgetMate.Application/Helpers/CoreDataSeedService.cs`, `src/HouseholdBudgetMate.Application/Auditing/AuditSaveChangesInterceptor.cs`, `src/HouseholdBudgetMate.Web/Program.cs`

**Intent**: Keep intentional startup provisioning functional after normal context fallback is removed, without causing audit records or seeded records to masquerade as user-driven pre-login activity.

**Contract**: Startup seed operations use an explicitly constructed technical-owner context. Audit scope resolving for ordinary interactive changes requires the active actor; any system/seed operation that remains auditable identifies its technical/system origin explicitly rather than relying on an empty-context fallback.

### Success Criteria:

#### Automated Verification:

- Session tests prove PIN validation is required for interactive sign-in, technical-owner cookies cannot restore a session, valid remembered visible-profile cookies still restore, and sign-out invalidates the cookie: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~UserSession"`
- Scope tests prove ordinary access without an active profile does not read or stamp `default-user` budget rows, while the explicit bootstrap/system path can still seed technical-owner records: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~UserScopingTests|FullyQualifiedName~Seed|FullyQualifiedName~Audit"`
- All affected projects compile after the session and scoping boundary changes: `dotnet build HouseholdBudgetMate.slnx`

#### Manual Verification:

- With no remembered session, open the application and confirm no budget dashboard, plan, account, loan, or archive data becomes visible before entering a correct PIN for a visible profile.
- Sign in successfully, reload the same browser session/profile, and confirm the accepted 30-day trusted restore opens the same permitted budget without displaying the technical owner.
- Sign out or corrupt/remove the remembered session and confirm the app returns to PIN selection without leaking prior household values.

**Implementation Note**: After completing this phase and all automated verification passes, pause for manual confirmation of the access boundary and explicitly accepted trusted-cookie behavior before completing administration and recovery workflows.

---

## Phase 3: Complete Profile Administration, Recovery, and Acceptance Evidence

### Overview

Make the administrator's day-to-day profile operations consistent with the hidden-owner design, provide a local recovery route, and establish the verification evidence required to release S-01 as a prerequisite for the monthly flow.

### Changes Required:

#### 1. Household Profile Administration UI

**File**: `src/HouseholdBudgetMate.Web/Components/Pages/AdminConfig.razor`

**Intent**: Let a PIN-authenticated administrator manage household member profiles, PINs, roles, and shared budget ownership without exposing the internal technical identity as an account to enter or edit.

**Contract**: The user-management view lists and creates only visible interactive profiles; creation requires a valid PIN; role management cannot leave the household without a visible secure administrator; shared-budget assignment can use the internal owner as a non-interactive underlying target where needed without offering it as a login identity; all wording referring to a PIN-less main account is removed.

#### 2. Administrative Recovery Flow

**Files**: supporting service/page under `src/HouseholdBudgetMate.Web/Setup/` and `src/HouseholdBudgetMate.Web/Components/Pages/`, with application service changes where PIN reset or admin promotion is performed

**Intent**: Provide the chosen local recovery mechanism when the only administrator forgets their PIN, without creating a login-screen bypass.

**Contract**: A locally reachable service/configuration recovery mode can establish or reset a PIN on a visible administrator linked to the technical owner, subject to the same 4-8 digit hashing rule. It is not selectable from ordinary profile login and normal budget routes remain blocked while recovery is required.

#### 3. Application and Session Verification Tests

**Files**: `src/HouseholdBudgetMate.Tests/Tests/Services/UserServiceAuthorizationTests.cs`, new focused session/setup/recovery tests under `src/HouseholdBudgetMate.Tests/Tests/`

**Intent**: Replace the existing protected bypass assumptions with executable proof of `FR-008`, `FR-009`, migration hardening, and the consciously retained trusted restore.

**Contract**: Tests cover visible admin profile creation and role guardrails, hashed PIN validation and invalid PIN rejection, exclusion/rejection of `default-user`, one-time hardening for legacy data, restore-cookie eligibility, local recovery of an inaccessible admin, and denial of budget scope without an eligible session.

### Success Criteria:

#### Automated Verification:

- Targeted access, setup, session, recovery, scoping, and audit tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~UserServiceAuthorizationTests|FullyQualifiedName~UserSession|FullyQualifiedName~Setup|FullyQualifiedName~Recovery|FullyQualifiedName~UserScopingTests|FullyQualifiedName~Audit"`
- Full application test suite passes without regressions: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj`
- Complete solution builds successfully: `dotnet build HouseholdBudgetMate.slnx`

#### Manual Verification:

- As a PIN-protected administrator, create a member profile with PIN, assign it shared household data access, switch to that member, and confirm it unlocks only after correct PIN entry and sees the assigned budget.
- Confirm the administrator can manage PINs and roles while `default-user` is absent from selectable login and editable household-profile UI.
- Exercise the local recovery path for a forgotten sole-admin PIN, then confirm ordinary login requires the recovered PIN and no pre-recovery budget content is shown.
- Confirm remembered-session behavior remains as agreed: after a valid PIN unlock the same browser may restore the eligible profile for up to 30 days; sign-out removes that access.

**Implementation Note**: After completing this phase and all automated verification passes, obtain manual confirmation that the administrator/member flow, recovery route, and remembered-session compromise are acceptable before implementation review.

---

## Testing Strategy

### Unit Tests:

- Update `UserServiceAuthorizationTests` to remove the PIN-less administrator expectation and cover hidden technical-owner behavior, secure administrator role invariants, and profile PIN management.
- Add focused tests for setup/access-hardening service behavior so clean installations and upgraded PIN-less installations both require a usable secure administrator.
- Add tests for `UserSessionService` with mocked JavaScript/data protection dependencies covering sign-in, trusted cookie restore, invalidation, and sign-out.
- Update `UserScopingTests` and audit tests for fail-closed interactive context and explicit system/bootstrap context.

### Integration Tests:

- Treat setup/hardening through session establishment and shared-budget scoping as an application-level integration path verified by tests across web services, application services, and EF query filters.
- Keep UI verification manual in this change because the project currently has no component or browser-test harness.

### Manual Testing Steps:

1. On a new configuration, create the first profile and confirm it is a PIN-protected administrator attached to the shared household budget.
2. On an upgraded database whose only administrator is `default-user`, confirm the application demands access hardening before showing any normal budget route.
3. Confirm `default-user` never appears in login or ordinary profile administration; create a new member and switch into its shared budget only after correct PIN entry.
4. Reload after a valid sign-in and confirm trusted-cookie restore works for the same eligible profile; sign out and confirm it no longer restores.
5. Simulate forgotten sole-admin PIN and complete local recovery; confirm subsequent login requires the new PIN and does not expose data beforehand.
6. Attempt an ordinary budget operation without active eligible session and confirm it fails closed rather than reading or writing technical-owner data.

## Performance Considerations

The added checks operate over a small household user list and a single readiness condition, consistent with the PRD's low scale. Session restoration should continue to resolve one remembered profile without introducing repeated per-page validation queries after session establishment. The fail-closed change must avoid forcing every data query through an additional database lookup once a valid scoped profile is already applied.

## Migration Notes

No migration should move existing budget records away from `default-user`; preserving it as the internal owner avoids rewriting all user-scoped financial tables. Implementation may require a schema or persisted configuration change only if distinguishing internal versus interactive profiles cannot be expressed safely using existing identity rules and durable hardening state; that decision should be reviewed before adding a migration. Existing installations must have an upgrade path that establishes a PIN-protected visible administrator before regular budget use resumes.

## References

- Change identity: `context/changes/verify-pin-gated-household-access/change.md`
- Roadmap item: `context/foundation/roadmap.md` (`S-01`)
- Product requirements: `context/foundation/prd.md` (`FR-008`, `FR-009`, `US-01`, `Access Control`)
- Existing user/PIN behavior: `src/HouseholdBudgetMate.Application/Services/UserService.cs:123`
- Existing session restoration: `src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:35`
- Existing login UI: `src/HouseholdBudgetMate.Web/Components/Dialogs/UserLoginDialog.razor:27`
- Existing setup: `src/HouseholdBudgetMate.Web/Setup/SetupConfigurationService.cs:76`
- Existing content gate: `src/HouseholdBudgetMate.Web/Components/Layout/MainLayout.razor:28`
- Existing scope fallback: `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:11`
- Existing access tests: `src/HouseholdBudgetMate.Tests/Tests/Services/UserServiceAuthorizationTests.cs:411`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` - <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Establish a Secure Administrator and Upgrade Gate

#### Automated

- [x] 1.1 The solution compiles after identity/setup contract changes: `dotnet build HouseholdBudgetMate.slnx` - 5f75f53
- [x] 1.2 User authorization tests prove technical-owner login is not supported and a secure visible administrator can act: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~UserServiceAuthorizationTests"` - 5f75f53
- [x] 1.3 Setup/hardening tests prove new and legacy installations establish a PIN-protected administrator before access: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~Setup|FullyQualifiedName~Access"` - 5f75f53

#### Manual

- [x] 1.4 Confirm fresh setup produces a PIN-protected administrator linked to the internal shared-budget owner with no selectable technical profile - 5f75f53
- [x] 1.5 Confirm a legacy PIN-less installation cannot reach ordinary pages until access hardening succeeds - 5f75f53

### Phase 2: Enforce PIN-Gated Sessions and Fail-Closed Budget Scope

#### Automated

- [x] 2.1 Session tests prove PIN sign-in, eligible trusted restore, technical-owner rejection, and sign-out invalidation: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~UserSession"` - fd579c3
- [x] 2.2 Scope and audit tests prove ordinary no-session access fails closed while explicit system bootstrap remains valid: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~UserScopingTests|FullyQualifiedName~Seed|FullyQualifiedName~Audit"` - fd579c3
- [x] 2.3 All affected projects compile after the session and scoping boundary changes: `dotnet build HouseholdBudgetMate.slnx` - fd579c3

#### Manual

- [x] 2.4 Confirm no budget content is visible before correct PIN entry when no remembered session exists - fd579c3
- [x] 2.5 Confirm an eligible remembered profile restores on the accepted trusted browser and no technical profile is shown - fd579c3
- [x] 2.6 Confirm sign-out or invalid remembered session returns to login without leaking prior household values - fd579c3

### Phase 3: Complete Profile Administration, Recovery, and Acceptance Evidence

#### Automated

- [x] 3.1 Targeted access, setup, session, recovery, scoping, and audit tests pass: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~UserServiceAuthorizationTests|FullyQualifiedName~UserSession|FullyQualifiedName~Setup|FullyQualifiedName~Recovery|FullyQualifiedName~UserScopingTests|FullyQualifiedName~Audit"` - 3422512
- [x] 3.2 Full application test suite passes without regressions: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj` - 3422512
- [x] 3.3 Complete solution builds successfully: `dotnet build HouseholdBudgetMate.slnx` - 3422512

#### Manual

- [x] 3.4 Confirm administrator creates a PIN-protected shared-budget member and that member unlocks assigned data only after correct PIN entry - 3422512
- [x] 3.5 Confirm technical owner is absent from login and editable household-profile administration UI - 3422512
- [x] 3.6 Confirm local sole-administrator recovery resets protected access without exposing data beforehand - 3422512
- [x] 3.7 Confirm remembered-session behavior and sign-out match the accepted 30-day trusted-browser policy - 3422512
