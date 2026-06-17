# Loan UI/UX Redesign Acceptance Evidence

## Automated Verification

- `dotnet build HouseholdBudgetMate.slnx` passed.
- `dotnet test HouseholdBudgetMate.slnx` passed.
- UI contract tests cover the loans workspace, schedule toolbar, bank update dialog, and empty-state prompt.

## Manual Verification

- [ ] Open `/loans` with no loans and confirm the empty state points to adding a loan.
- [ ] Add a mortgage and confirm it becomes selectable.
- [ ] Select an active loan and inspect KPI values.
- [ ] Switch between tabs and confirm the selected loan remains stable.
- [ ] Filter the schedule to unpaid/future installments.
- [ ] Mark a payment paid and unpaid.
- [ ] Open prepayment, bank installment amount change, and edit installment workflows.
- [ ] Add a WIBOR entry and verify schedule refresh behavior still works.
- [ ] Add, deactivate, reactivate, and delete a loan cost.
- [ ] Check desktop, tablet, mobile, and dark mode.

## Phase 7 Scenario

- Real mortgage-like workflow to verify:
  - Start 800000
  - Initial WIBOR 3.8
  - Change to 3.73
  - Update from bank installment amount and last installment date
