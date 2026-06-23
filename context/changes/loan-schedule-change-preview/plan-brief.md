# Loan Schedule Change Preview — Plan Brief

> Full plan: `context/changes/loan-schedule-change-preview/plan.md`

## What & Why

Add a mandatory preview and confirmation dialog before a prepayment, WIBOR update, or bank-provided installment change rebuilds the loan schedule. The user can verify the complete before/after schedule, return to correct inputs, and save only after the recalculation is accepted.

The financial algorithm is locked: formulas, rounding and all current principal, interest, installment and cost results must remain unchanged.

## Starting Point

The three workflows currently save immediately. Existing service methods already contain the authoritative calculations and synchronization side effects, while the loan page already has input dialogs/tabs and a componentized schedule table.

## Desired End State

Each workflow becomes: enter data, calculate without saving, review a full year-grouped comparison, then confirm or return to editing. Confirmation is rejected if the loan schedule changed after preview. Canceling or previewing has no database or audit effects.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Financial calculations | Reuse existing algorithm unchanged | Preview must not change trusted mortgage results or rounding. |
| Covered operations | Prepayment, WIBOR and bank installment/period change | These three actions rebuild future schedules. |
| Commit timing | Save only after explicit confirmation | Users can verify calculations before financial data changes. |
| Schedule scope | Complete before/after schedule | The user wants to audit every affected installment. |
| Long-schedule UX | Collapsible yearly sections | Prevents a multi-decade schedule from breaking the dialog layout. |
| Initial expansion | First affected year only | Gives immediate context while keeping the DOM and visual load small. |
| Summary | Principal, next installment, total interest, end date, installment count | These values explain the overall consequence before row-level review. |
| Editing | Return to original inputs with values preserved | Correction does not require retyping data. |
| Stale preview | Reject and require recalculation | Prevents confirming a proposal based on obsolete schedule data. |
| Persistence | No preview storage or migration | Preview is transient and based on current persisted state. |

## Scope

**In scope:**

- Side-effect-free previews for three schedule-changing operations.
- Shared comparison DTOs and opaque source schedule version.
- Full before/after rows and summaries.
- Year-grouped MudBlazor dialog with responsive tables.
- Preview/edit/confirm orchestration and stale-state handling.
- Financial parity, side-effect and UI workflow tests.

**Out of scope:**

- Any change to amortization formulas or expected numeric fixtures.
- Manual single-installment override preview.
- Database migration, persisted drafts, undo after save, charts or exports.

## Architecture / Approach

Refactor only the orchestration around the existing loan calculations: an internal projection routine calculates the proposal, preview maps it without saving, and confirmation persists a fresh projection after checking the reviewed source version. `Loans.razor` coordinates one shared preview dialog for WIBOR, prepayment and bank-update forms.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Projection contracts | Shared calculation path, DTOs and stale guard | Accidentally duplicating or changing financial logic. |
| 2. Comparison dialog | Summary and full year-grouped before/after schedule | Long schedules causing layout or DOM issues. |
| 3. Workflow integration | Preview/edit/confirm for all three operations | Losing inputs or triggering duplicate writes. |
| 4. Regression verification | Numerical parity and acceptance evidence | Missing a rounding or synchronization edge case. |

**Prerequisites:** Current loan calculation tests remain the source of truth and their expected values are frozen.

**Estimated effort:** About 3–4 focused implementation sessions across four phases.

## Open Risks & Assumptions

- Refactoring calculation from persistence must avoid mutating tracked entities during preview.
- The source-version fingerprint must cover every persisted input that affects schedule results.
- Full schedule data remains in memory, but collapsed years must not keep their tables alive in the DOM.
- Existing month-plan and audit side effects run only after successful confirmation.

## Success Criteria (Summary)

- Preview and confirmed schedules are identical for WIBOR, both prepayment strategies and bank shortening.
- Existing financial expected values remain unchanged and the full test suite passes.
- A long schedule is readable by year, inputs survive return-to-edit, and stale previews cannot be saved.
