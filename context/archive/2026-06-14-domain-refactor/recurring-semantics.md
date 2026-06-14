# Recurring Generated Rows

The recurring-month duplicate check intentionally uses `IgnoreQueryFilters()` for generated expense rows. A soft-deleted recurring expense should still block the same regular definition from being regenerated into that month.

Why this matters:
- It preserves the current user-visible rule that deleting a generated recurring item does not silently bring it back in the same month.
- It keeps the month-generation path aligned with the existing duplicate-detection behavior in `ExpenseService`.

Covered by:
- `DeleteRecurringExpense_FromMonth_Should_Not_Recreate_And_Should_Not_Throw_On_Reload`
- `AddRegularExpenseDefinitionToMonthAsync_Should_Return_False_When_SoftDeleted_Generated_Row_Exists`
