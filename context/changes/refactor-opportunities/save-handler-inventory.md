# Save Handler Inventory

Baseline inventory for Phase 1 of `refactor-opportunities`.

## Refresh Modes

- `full reload`: the handler calls `LoadAsync()`.
- `preparation-bypass reload`: the handler calls `LoadAsync(bypassPreparation: true)`.
- `target-copy no current-month reload`: the handler intentionally does not reload the source month after copying.
- `line-item re-expand`: the handler reloads and then restores the expanded expense row.
- `month close/open reload`: the handler closes or opens the month, refreshes archive cache, then reloads.
- `warning / validation early return`: the handler exits before any service call.

## PlanPage.Expenses.cs

### `CreateExpenseAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:102-134`
- Preconditions: `EnsureMonthEditable()`, then planned/actual amount parsing.
- Service call: `ExpenseService.CreateExpenseAsync`.
- Local cleanup: `ResetCreateExpenseForm()`.
- Refresh mode: `full reload`.
- Snackbar outcome: success on save, error on exception.
- Early return: editability guard and amount parsing failures.

### `SaveEditAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:191-220`
- Preconditions: `EnsureMonthEditable()` and `_editExpense is not null`, then amount parsing.
- Service call: `ExpenseService.UpdateExpenseAsync`.
- Local cleanup: `CancelEdit()`.
- Refresh mode: `full reload`.
- Snackbar outcome: success on save, error on exception.
- Early return: editability guard, missing edit state, and amount parsing failures.

### `DeleteExpenseAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:222-246`
- Preconditions: `EnsureMonthEditable()`, then delete confirmation.
- Service call: `ExpenseService.DeleteExpenseAsync`.
- Local cleanup: none besides reload state.
- Refresh mode: `full reload`.
- Snackbar outcome: success on delete, error on exception.
- Early return: editability guard and canceling confirmation.

### `MoveExpenseAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:248-290`
- Preconditions: `EnsureMonthEditable()`, no active expense filters, and the requested row exists.
- Service call: `ExpenseService.ReorderExpensesAsync`.
- Local cleanup: none besides the reload.
- Refresh mode: `full reload`.
- Snackbar outcome: info when filters block reorder, error on exception, no explicit success snackbar.
- Early return: editability guard, active filters, missing expense id, out-of-range movement, and exceptions.

### `CopySelectedExpensesAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:318-373`
- Preconditions: at least one selected expense, target month differs from source month, and delete confirmation.
- Service call: `ExpenseService.CopySelectedExpensesToMonthAsync`.
- Local cleanup: `_selectedExpenseIdsForCopy.Clear()` and `_isCopyMode = false`.
- Refresh mode: `target-copy no current-month reload`.
- Snackbar outcome: warning for empty selection / same month, success or info after copy, error on exception.
- Early return: validation warnings and confirmation cancelation.

### `ApplyMonthPlanSuggestionsAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:397-467`
- Preconditions: `HasMonthPreparation`, at least one selected available suggestion, and amount parsing for each selected draft.
- Service call: `ExpenseService.ApplyMonthPlanSuggestionsAsync`.
- Local cleanup: `_isCopyMode = false`, `_selectedExpenseIdsForCopy.Clear()`, `ClearMonthPreparation()`.
- Refresh mode: `preparation-bypass reload`.
- Snackbar outcome: warning for empty selection, success / info after apply, error on exception.
- Early return: missing preparation, no selected suggestions, amount parsing failure.

### `SkipMonthPlanSuggestionsAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:469-490`
- Preconditions: `HasMonthPreparation`.
- Service call: none.
- Local cleanup: `_isCopyMode = false`, `_selectedExpenseIdsForCopy.Clear()`, `ClearMonthPreparation()`.
- Refresh mode: `preparation-bypass reload`.
- Snackbar outcome: info on skip, error on exception.
- Early return: missing preparation.

## PlanPage.Incomes.cs

### `CreateIncomeAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Incomes.cs:84-117`
- Preconditions: `EnsureMonthEditable()` and amount parsing.
- Service call: `IncomeService.CreateIncomeAsync`.
- Local cleanup: reset new-income fields and defaults.
- Refresh mode: `full reload`.
- Snackbar outcome: success on save, error on exception.
- Early return: editability guard and amount parsing failures.

### `SaveIncomeEditAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Incomes.cs:144-171`
- Preconditions: `EnsureMonthEditable()` and `_editIncome is not null`, then amount parsing.
- Service call: `IncomeService.UpdateIncomeAsync`.
- Local cleanup: clear edit state and reset amount input.
- Refresh mode: `full reload`.
- Snackbar outcome: success on save, error on exception.
- Early return: editability guard, missing edit state, and amount parsing failures.

### `DeleteIncomeAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Incomes.cs:173-196`
- Preconditions: `EnsureMonthEditable()` and delete confirmation.
- Service call: `IncomeService.DeleteIncomeAsync`.
- Local cleanup: none besides reload state.
- Refresh mode: `full reload`.
- Snackbar outcome: success on delete, error on exception.
- Early return: editability guard and canceling confirmation.

## PlanPage.SavingsTransfers.cs

### `CreateSavingsTransferAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.SavingsTransfers.cs:9-40`
- Preconditions: `EnsureMonthEditable()` and amount parsing.
- Service call: `ExpenseService.CreateMonthSavingsTransferItemAsync`.
- Local cleanup: reset transfer amount and date defaults.
- Refresh mode: `full reload`.
- Snackbar outcome: success on save, error on exception.
- Early return: editability guard and amount parsing failures.

### `SaveSavingsTransferEditAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.SavingsTransfers.cs:63-90`
- Preconditions: `EnsureMonthEditable()` and `_editSavingsTransfer is not null`, then amount parsing.
- Service call: `ExpenseService.UpdateMonthSavingsTransferItemAsync`.
- Local cleanup: clear edit state and reset amount input.
- Refresh mode: `full reload`.
- Snackbar outcome: success on save, error on exception.
- Early return: editability guard, missing edit state, and amount parsing failures.

### `DeleteSavingsTransferAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.SavingsTransfers.cs:92-110`
- Preconditions: `EnsureMonthEditable()`.
- Service call: `ExpenseService.DeleteMonthSavingsTransferItemAsync`.
- Local cleanup: none besides reload state.
- Refresh mode: `full reload`.
- Snackbar outcome: success on delete, error on exception.
- Early return: editability guard.

## PlanPage.LineItems.cs

### `CreateLineItemAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:36-78`
- Preconditions: `EnsureMonthEditable()`, nonblank description, and amount parsing.
- Service call: `ExpenseService.CreateExpenseLineItemAsync`.
- Local cleanup: reset the create model and amount input.
- Refresh mode: `full reload` followed by line-row re-expansion.
- Snackbar outcome: success on save, warning for blank description, error on exception.
- Early return: editability guard, blank description, and amount parsing failures.

### `SaveLineItemEditAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:96-128`
- Preconditions: `EnsureMonthEditable()`, `_editLineItem is not null`, then amount parsing.
- Service call: `ExpenseService.UpdateExpenseLineItemAsync`.
- Local cleanup: `CancelLineItemEdit()`.
- Refresh mode: `full reload` followed by line-row re-expansion.
- Snackbar outcome: success on save, error on exception.
- Early return: editability guard, missing edit state, and amount parsing failures.

### `DeleteLineItemAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:139-162`
- Preconditions: `EnsureMonthEditable()`.
- Service call: `ExpenseService.DeleteExpenseLineItemAsync`.
- Local cleanup: none besides reload state.
- Refresh mode: `full reload` followed by line-row re-expansion when `expenseId > 0`.
- Snackbar outcome: success on delete, error on exception.
- Early return: editability guard.

## PlanPage.Lifecycle.cs

### `LoadAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:25-103`
- Role: central page reload hub for categories, tag usage, accounts, month preparation, month plan, dashboard summary, incomes, live balance, chart state, selection state, and dirty tracking.
- Refresh mode: baseline reload or `preparation-bypass reload` depending on the argument.
- Early return: preparation-only branch when the month does not exist and suggestions are available.

### `ToggleMonthStatusAsync`

- Evidence: `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Lifecycle.cs:210-239`
- Preconditions: confirmation only for closing a month.
- Service call: `ExpenseService.CloseMonthAsync` or `ExpenseService.OpenMonthAsync`.
- Local cleanup: archive cache refresh before reloading.
- Refresh mode: `month close/open reload`.
- Snackbar outcome: success on close/open, error on exception.
- Early return: confirmation cancelation on close.
