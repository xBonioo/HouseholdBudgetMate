# Loan Operation Revert Implementation Plan

## Overview

Add an MVP revert capability for loan operations, scoped to:

- Loan prepayment operations.
- WIBOR/rate-entry operations.

The user starts from the audit history and selects Revert on a supported loan operation. The system validates that the loan has not changed since that operation, applies the inverse operation atomically, syncs affected open month plans, and records a new audit trail entry explaining the revert.

This plan intentionally does not build a general-purpose audit-log revert framework. The current audit trail is entity-level, while loan changes are business operations that affect multiple entities. The implementation should add an operation-level audit model for loan operations and leave ordinary entity audit rows intact.

## Decisions

| Decision | Choice |
| --- | --- |
| Scope | Revert prepayment and WIBOR/rate changes only. |
| UI entry point | Audit UI. |
| Prepayment revert | Undo prepayment record, schedule rebuild, and generated prepayment expense. |
| WIBOR revert | Remove the specific `LoanRateEntry` and rebuild schedule from its effective date. |
| Later changes | Block revert if the current schedule no longer matches the operation's post-change version. |
| Revert audit | Create explicit `Revert` audit trail linked to the reverted operation. |
| Authorization | Any user with access to the budget can revert; admin-only is not required. |

## Current State Analysis

### Loan schedule operations

- `src/HouseholdBudgetMate.Application/Services/LoanService.cs` contains preview and confirm methods for supported schedule changes:
  - `PreviewAddLoanRateEntryAsync`
  - `PreviewApplyLoanPrepaymentAsync`
  - `AddLoanRateEntryAsync`
  - `ApplyLoanPrepaymentAsync`
- These methods already compute and validate `ExpectedScheduleVersion`.
- `ApplyLoanPrepaymentAsync` records a `LoanPrepayment`, updates/rebuilds installments, and upserts a prepayment expense.
- `AddLoanRateEntryAsync` appends a `LoanRateEntry`, persists projected installments, and syncs affected month plans.
- `SyncOpenLoanInstallmentPlansAsync` and `SyncLoanInstallmentsForMonthAsync` are already part of the write path and should be reused after revert.

### Audit model

- `AuditSaveChangesInterceptor` creates `AuditLog` rows from EF changes.
- Audit entries are entity-level, not operation-level.
- Current auditable loan coverage includes `LoanInstallment` modifications, but not `Loan`, `LoanRateEntry`, `LoanPrepayment`, or `LoanCharge`.
- `AuditService.SearchAsync` returns `AuditLogDto` rows and enriches entity context for display.
- `Audit.razor` is currently admin-only and displays audit details with filters.

### Risk in naive revert

Reverting one `AuditLog` row for `LoanInstallment` would not reverse the full loan operation. One prepayment or WIBOR change can update many installments and related expenses. Revert must target a grouped business operation.

## Target Architecture

### New operation audit concept

Add a durable operation-level record for loan operations. Suggested entity name:

- `LoanOperationAudit`

Suggested core fields:

- `Id`
- `LoanId`
- `BudgetOwnerUserId`
- `UserId`
- `OperationType`
- `Status`
- `OccurredAtUtc`
- `ScheduleVersionBefore`
- `ScheduleVersionAfter`
- `OperationPayloadJson`
- `RevertedAtUtc`
- `RevertedByUserId`
- `RevertsOperationId`
- `RevertedByOperationId`

Recommended operation types:

- `LoanPrepayment`
- `LoanRateEntry`
- `LoanOperationRevert`

Recommended status values:

- `Active`
- `Reverted`

`OperationPayloadJson` should contain enough data to perform the inverse safely:

For prepayment:
- `loanPrepaymentId`
- `loanInstallmentId` or due date/month identifying the original target
- `amount`
- `prepaymentDate`
- `strategy`
- `scheduleStart`
- `scheduleEnd`
- generated prepayment expense identity if one was created or updated

For rate entry:
- `loanRateEntryId`
- `effectiveFrom`
- `referenceRate`
- `scheduleStart`
- `scheduleEnd`

The operation record should be created inside the same transaction as the loan write, after the before/after schedule versions are known.

### Revert command model

Add contracts in `HouseholdBudgetMate.Abstractions`:

- `RevertLoanOperationRequest`
- `LoanOperationAuditDto` or audit DTO additions that expose revertability

The command should target `LoanOperationAudit.Id`, not an entity audit log id.

Suggested service surface:

- Add to `ILoanService`:
  - `Task<LoanDto> RevertLoanOperationAsync(RevertLoanOperationRequest request, CancellationToken cancellationToken);`
- Add to `IAuditService` or a new loan operation query method:
  - expose supported loan operation rows to audit UI with `CanRevert`, `RevertBlockedReason`, and operation context.

Prefer keeping the write command on `ILoanService`, because revert changes the loan domain and needs the same schedule/version logic.

## Phase 1: Operation Audit Model And Contracts

### Goals

Create the durable concept of a revertable loan operation without changing UI behavior yet.

### Changes Required

#### `src/HouseholdBudgetMate.Domain/Entities/LoanOperationAudit.cs`

**Intent:** Add an operation-level audit entity that groups the effects of one loan business operation.

**Contract:** Entity includes loan id, user scope, operation type/status, before/after schedule versions, payload JSON, timestamps, and revert linkage.

#### `src/HouseholdBudgetMate.Domain/EntityConfiguration/LoanOperationAuditConfiguration.cs`

**Intent:** Configure persistence, required fields, max lengths, JSON column, relationships, and indexes.

**Contract:** Index `BudgetOwnerUserId`, `LoanId`, `OperationType`, `Status`, `OccurredAtUtc`, and unique/foreign-key relation for revert linkage where supported by EF.

#### `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs`

**Intent:** Expose `DbSet<LoanOperationAudit>`.

**Contract:** Add `public DbSet<LoanOperationAudit> LoanOperationAudits { get; set; }`.

#### EF migration

**Intent:** Create the `LoanOperationAudits` table.

**Contract:** Migration should preserve existing audit logs and not attempt to backfill revertable operations for old history.

#### `src/HouseholdBudgetMate.Abstractions/Contracts/Loans/Requests/RevertLoanOperationRequest.cs`

**Intent:** Add the command contract for reverting one operation.

**Contract:** Include `LoanOperationAuditId` and optional `ExpectedScheduleVersion`; the backend may derive the expected version from the operation record, but the request can carry it for UI consistency.

#### `src/HouseholdBudgetMate.Abstractions/Contracts/Audit/Dto`

**Intent:** Give the audit UI enough information to show a Revert action.

**Contract:** Add either a separate `LoanOperationAuditDto` or extend `AuditLogDto` with optional fields:
- `LoanOperationAuditId`
- `IsRevertable`
- `CanRevert`
- `RevertBlockedReason`
- `LoanOperationType`

Prefer a separate DTO if `AuditLogDto` would become ambiguous.

### Success Criteria

#### Automated Verification

- `dotnet build HouseholdBudgetMate.slnx -c Release` succeeds.
- Migration snapshot includes `LoanOperationAudits`.
- Tests prove creating a prepayment and a rate-entry operation records one operation audit row with before/after schedule versions.

#### Manual Verification

- Existing audit page still loads with old entity-level audit logs.
- Existing loan prepayment and WIBOR flows behave the same before any revert UI is added.

## Phase 2: Backend Revert Behavior

### Goals

Implement safe, atomic revert for prepayment and rate-entry operations.

### Changes Required

#### `src/HouseholdBudgetMate.Application/Services/LoanService.cs`

**Intent:** Record operation audit rows during supported writes and implement `RevertLoanOperationAsync`.

**Contract:**
- `AddLoanRateEntryAsync` records `LoanRateEntry` operation with before/after versions.
- `ApplyLoanPrepaymentAsync` records `LoanPrepayment` operation with before/after versions and prepayment expense metadata.
- `RevertLoanOperationAsync` loads the operation and loan under the current budget scope.
- Revert validates:
  - operation exists and belongs to current budget owner,
  - operation type is supported,
  - operation is not already reverted,
  - current schedule version equals `ScheduleVersionAfter`,
  - loan still exists,
  - referenced rate/prepayment record still exists.
- Revert blocks with `ConflictException` when the schedule version differs.

#### Prepayment inverse

**Intent:** Undo the prepayment and all direct effects.

**Contract:**
- Remove or mark the specific `LoanPrepayment` as reverted/deleted.
- Remove or restore the generated prepayment expense based on payload metadata.
- Rebuild installments from the prepayment point forward as if the prepayment never happened.
- Preserve paid status for unaffected past installments where current schedule rules allow.
- Re-sync open month plans for the affected schedule range.
- Mark source operation `Reverted`.
- Add a new `LoanOperationRevert` operation linked to the source.

Implementation note: if hard deleting `LoanPrepayment` makes audit history hard to explain, add `IsReverted`, `RevertedAtUtc`, and `RevertedByOperationId` to `LoanPrepayment` instead. Prefer soft state for traceability unless existing query assumptions strongly favor deletion.

#### Rate-entry inverse

**Intent:** Undo the exact WIBOR/rate entry and rebuild downstream schedule.

**Contract:**
- Remove or mark reverted the `LoanRateEntry` referenced by the operation payload.
- Rebuild installments from `EffectiveFrom` forward.
- Re-sync open month plans for affected range.
- Mark source operation `Reverted`.
- Add a new `LoanOperationRevert` operation linked to the source.

Implementation note: for traceability, prefer `IsReverted` on `LoanRateEntry` if the UI can hide reverted entries cleanly. If hard delete is chosen, the operation audit payload must remain sufficient to explain what was removed.

#### Authorization and scope

**Intent:** Allow non-admin budget users to revert while preventing cross-budget actions.

**Contract:**
- Revert command must require authenticated current user.
- Query must ensure the loan and operation belong to `currentUserContext.BudgetOwnerUserId`.
- Audit UI access changes must not expose unrelated budget logs.

#### Audit trace

**Intent:** Make revert visible and explainable.

**Contract:**
- Operation audit records `LoanOperationRevert`.
- Existing entity-level audit logs may still be generated for changed installments/expenses.
- The audit UI should be able to show "Reverted by ..." and "Reverts operation #...".

### Success Criteria

#### Automated Verification

- Service test: reverting prepayment restores the same schedule version as before the prepayment.
- Service test: reverting prepayment removes/restores generated prepayment expense and syncs open month plans.
- Service test: reverting WIBOR/rate entry restores the same schedule version as before rate change.
- Service test: revert creates a `LoanOperationRevert` row and marks source operation reverted.
- Service test: revert is blocked if current schedule version differs from `ScheduleVersionAfter`.
- Service test: user from another budget cannot revert the operation.

#### Manual Verification

- Create loan, apply prepayment, verify changed schedule, revert, verify schedule and month plan are back.
- Add WIBOR/rate entry, verify changed schedule, revert, verify rate list and schedule are back.
- Apply another loan change after the target operation and confirm revert is blocked.

## Phase 3: Audit UI Integration

### Goals

Expose revert from the audit workflow with clear confirmation and blocked-state feedback.

### Changes Required

#### `src/HouseholdBudgetMate.Application/Services/AuditService.cs`

**Intent:** Include loan operation audit data in audit results or provide a companion query for audit page display.

**Contract:**
- Return operation rows for `LoanPrepayment` and `LoanRateEntry`.
- Include display context: loan name, operation type, amount/rate, date, actor, and changed time.
- Include revert state:
  - available,
  - already reverted,
  - stale due to later changes,
  - unsupported operation.

#### `src/HouseholdBudgetMate.Web/Components/Pages/Audit.razor`

**Intent:** Show Revert action for supported loan operation rows.

**Contract:**
- Add action column or expanded-row action.
- Revert button visible for supported loan operation rows.
- Button disabled with reason when already reverted or stale.
- Confirmation dialog explains exactly what will be reverted:
  - prepayment amount/date and affected loan,
  - WIBOR/rate effective date and rate,
  - warning that later changes block revert.
- On success, refresh audit list and show success snackbar.
- On conflict, show warning snackbar and refresh row state.

#### UI routing/access

**Intent:** Non-admin budget users need access to the revert entry point without exposing unrelated admin audit controls.

**Contract:**
- Either split audit into:
  - admin audit history view,
  - loan operation history/revert view available to budget users;
- or adjust `/admin/audit` carefully so non-admin users can only see loan operation rows for their budget.

Recommendation: create a loan operation history section/page under loans or audit-like route for budget users if broadening `/admin/audit` would blur admin boundaries. The user asked for auditlogs action, so the page can still be audit-backed while not exposing full admin audit.

#### Tests

**Intent:** Protect the UX contract with focused bUnit/string-contract tests.

**Contract:**
- Audit/loan operation UI shows Revert for prepayment and WIBOR operations.
- Revert action calls `ILoanService.RevertLoanOperationAsync`.
- Disabled states and blocked reasons render.
- Non-admin budget user can access only the supported loan operation revert view, not full admin audit history.

### Success Criteria

#### Automated Verification

- UI tests prove the revert button appears only for supported operation rows.
- UI tests prove confirmation calls the revert service and refreshes.
- UI tests prove stale/already-reverted rows are disabled with explanation.
- Existing audit admin tests still pass.
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release` succeeds.

#### Manual Verification

- As regular budget user, find a loan operation and revert it.
- As admin, still use existing audit filters.
- As user from another budget, verify no access to other budget operation rows.

## Testing Strategy

### Unit and Service Tests

- Extend `LoanServiceTests` for:
  - operation audit row creation for prepayment,
  - operation audit row creation for rate entry,
  - successful prepayment revert,
  - successful rate-entry revert,
  - stale schedule conflict,
  - already reverted conflict,
  - budget-scope authorization.
- Extend `AuditTrailTests` or add `LoanOperationAuditTests` for:
  - revert audit trace,
  - display metadata,
  - old entity audit logs remain readable.

### UI Tests

- Extend `LoanUiRedesignTests` or add `LoanOperationRevertUiTests`.
- Validate:
  - Revert button text and confirmation copy,
  - disabled stale state,
  - regular user access behavior,
  - admin audit remains admin-only if a separate operation view is added.

### Manual Test Script

1. Create or select a loan with future installments.
2. Apply a prepayment and confirm schedule changes.
3. Open loan operation history/audit and revert the prepayment.
4. Verify schedule, prepayment record visibility, and month-plan expense state.
5. Add a WIBOR/rate entry and confirm schedule changes.
6. Revert the rate entry and verify schedule/rate list.
7. Apply another loan change after a target operation and verify the older operation cannot be reverted.

## Performance Considerations

- Revert should use existing schedule projection/persistence logic; expected scale is one loan schedule at a time.
- Audit query should not join all entity audit logs with operation payloads unnecessarily. Use indexed `LoanOperationAudits` filters and load related loan names in batch.
- JSON payloads are acceptable for operation metadata because revert scope is narrow and operation-specific.

## Migration Notes

- Existing `AuditLogs` remain unchanged.
- Existing historical loan changes will not be revertable because they lack operation audit metadata.
- New `LoanOperationAudits` rows are created only after deployment.
- If soft-revert fields are added to `LoanPrepayment` and `LoanRateEntry`, default existing rows to active/non-reverted.

## Rollback Notes

- Rolling back the feature should not delete existing `AuditLogs`.
- If `LoanOperationAudits` has been populated in production, retain the table or migrate it to archive rather than dropping financial history blindly.
- Revert operations themselves are financial history and must not be cleaned by operational log retention.

## References

- Loan service schedule write paths: `src/HouseholdBudgetMate.Application/Services/LoanService.cs`
- Loan service interface: `src/HouseholdBudgetMate.Abstractions/Interfaces/ILoanService.cs`
- Audit service interface: `src/HouseholdBudgetMate.Abstractions/Interfaces/IAuditService.cs`
- Audit interceptor: `src/HouseholdBudgetMate.Application/Auditing/AuditSaveChangesInterceptor.cs`
- Audit page: `src/HouseholdBudgetMate.Web/Components/Pages/Audit.razor`
- Loan page: `src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor`
- Loan tests: `src/HouseholdBudgetMate.Tests/Tests/Services/LoanServiceTests.cs`
- Audit tests: `src/HouseholdBudgetMate.Tests/Tests/Services/AuditTrailTests.cs`

## Progress

### Phase 1: Operation Audit Model And Contracts

#### Automated

- [x] 1.1 `dotnet build HouseholdBudgetMate.slnx -c Release` succeeds.
- [x] 1.2 Migration snapshot includes `LoanOperationAudits`.
- [x] 1.3 Tests prove supported loan writes create operation audit rows with before/after schedule versions.

#### Manual

- [ ] 1.4 Existing audit page still loads.
- [ ] 1.5 Existing loan prepayment and WIBOR flows behave the same before revert UI.

### Phase 2: Backend Revert Behavior

#### Automated

- [x] 2.1 Reverting prepayment restores the pre-operation schedule version.
- [x] 2.2 Reverting prepayment restores generated prepayment expense/month-plan effects.
- [x] 2.3 Reverting WIBOR/rate entry restores the pre-operation schedule version.
- [x] 2.4 Revert creates a `LoanOperationRevert` audit row and marks the source operation reverted.
- [x] 2.5 Revert is blocked when current schedule differs from the operation after-version.
- [x] 2.6 Cross-budget revert is rejected.

#### Manual

- [ ] 2.7 Manual prepayment revert restores loan schedule and month plan.
- [ ] 2.8 Manual WIBOR/rate revert restores loan schedule and rate list.
- [ ] 2.9 Later loan change blocks older revert with clear conflict.

### Phase 3: Audit UI Integration

#### Automated

- [x] 3.1 UI tests show Revert only for supported loan operation rows.
- [x] 3.2 UI tests confirm revert calls the loan service and refreshes.
- [x] 3.3 UI tests render disabled stale/already-reverted states with reason.
- [x] 3.4 Existing audit admin tests still pass.
- [x] 3.5 `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release` succeeds.

#### Manual

- [ ] 3.6 Regular budget user can revert supported loan operations.
- [ ] 3.7 Admin audit filters still work.
- [ ] 3.8 User from another budget cannot see or revert another budget's loan operations.
