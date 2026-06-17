# Loan UI/UX Redesign - Plan Brief

> Full plan: `context/changes/loan-ui-ux-redesign/plan.md`

## What & Why

Redesign the loan management screen so it feels like a clear servicing workspace for an active loan, not a long mixed form. The user should be able to pick a loan, understand its current state, inspect the schedule, update bank-provided installment values, and manage costs/WIBOR without hunting through a crowded page.

## Starting Point

`src/HouseholdBudgetMate.Web/Components/Pages/Loans.razor` currently owns creation, list, edit forms, WIBOR updates, costs, schedule, row actions, and dialogs in one large component. The existing backend and DTOs already support the needed workflows, including bank installment amount changes, so this plan intentionally keeps the backend unchanged.

## Desired End State

The loans page is centered on the selected active loan. It has a KPI summary, tabbed servicing areas, a filtered schedule table with restrained row actions, and a clearer "bank schedule update" workflow. The code is split into focused frontend components so future UX changes are easier and safer.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Primary workflow | Active-loan servicing | The screen is used repeatedly for one real mortgage, not only initial setup. |
| Edit structure | Tabs | Separates schedule, WIBOR, costs, and settings without one long mixed form. |
| Installment list | Table with toolbar and filters | A table best supports comparing months, principal, interest, costs, and status. |
| Row actions | One primary action plus menu | Keeps the dense schedule readable while retaining all existing operations. |
| Bank installment change | Dedicated workflow | The action changes future schedule state, so it deserves clearer context and confirmation. |
| Loan overview | KPI summary | Users need immediate orientation: next payment, remaining principal, rate, and end date. |
| Refactor scope | Componentized frontend redesign | This improves maintainability while honoring the backend-free constraint. |

## Scope

**In scope:** componentizing the loan page, new selected-loan layout, tabs, schedule toolbar/filters, action menu, improved bank schedule update dialog/panel, responsive styling, and UI verification.

**Out of scope:** backend contracts, loan algorithms, database schema, migrations, new financial calculations, and changes to plan/month synchronization behavior.

## Approach

Keep `Loans.razor` as the routed orchestration page, but move coherent UI surfaces into loan-specific components under the web project. The parent keeps data loading and service calls; child components receive DTOs and raise callbacks for existing actions.

## Phases At A Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Component Baseline | Splits the current page into focused UI components without behavior changes. | Accidentally changing event flow while extracting markup. |
| 2. Active Loan Workspace | Introduces selected-loan summary and moves creation out of the default visual path. | Losing discoverability for adding a new loan. |
| 3. Tabbed Servicing | Organizes schedule, WIBOR, costs, and settings into tabs. | Dirty-state behavior across tabs must remain predictable. |
| 4. Schedule Table UX | Adds toolbar filters and simplified row actions. | Dense table must stay usable on mobile and desktop. |
| 5. Bank Update Workflow | Makes "change installment from bank" clearer and safer. | Copy must explain impact without implying new backend validation. |
| 6. Polish & Responsive QA | Finishes spacing, states, dark mode, and responsive behavior. | Layout regressions from page-specific CSS. |
| 7. Verification | Builds/tests and manually validates key loan workflows. | Existing UI test coverage may be thin. |

**Prerequisites:** Existing loan service behavior remains stable; no backend refactor is required.
**Estimated effort:** Medium-large frontend change, around 2-4 focused implementation sessions.

## Open Risks & Assumptions

- The current single-file component may have intertwined dirty-state and dialog state that needs careful extraction.
- MudBlazor is the existing UI framework; this plan assumes no new component library.
- The schedule may contain hundreds of rows, so desktop density and mobile fallback both need manual verification.

## Success Criteria Summary

- A user can manage a real mortgage from the selected-loan workspace without scrolling through unrelated creation fields.
- The schedule is easier to scan, filter, and act on, with the same backend operations available.
- The redesign is frontend-only and verified by build/tests plus manual walkthroughs.
