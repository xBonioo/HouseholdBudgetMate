# Acceptance Evidence - Improve Monthly Planning

> Change: `improve-monthly-planning`
> Date: 2026-06-04
> Scope: S-03 monthly preparation, targeted expense copy, annual planning targets, and alert-prep candidates.

## Automated Verification

| Check | Command | Result | Notes |
| --- | --- | --- | --- |
| Targeted planning tests | `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~MonthlyBudgetingLoopUiTests"` | Passed: 76/76 tests | Covers service math, monthly loop projections, and static UI wiring. |
| Full release suite | `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release` | Passed: 345/345 tests | Includes setup/readiness and architecture-adjacent coverage. |
| Release build | `dotnet build HouseholdBudgetMate.slnx -c Release` | Passed | Existing MudBlazor analyzer warning remains for `PlanPage.razor` `Dense` usage. |
| Git whitespace | `git diff --check -- .` | Passed | Git reported CRLF normalization warnings only. |
| Architecture sanity check | `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~Architecture"` | Passed: 23/23 tests | The older `FullyQualifiedName~ArchitectureTests` filter matched zero tests. |

## Service Evidence

- First-open preparation preview is covered by `GetMonthPlanPreparationAsync_Should_Not_Create_Target_Month_And_Mark_Recurring_Suggestion_Unavailable`.
- Active recurring matching for older manual history is covered by `GetMonthPlanPreparationAsync_Should_Mark_Manual_History_Unavailable_When_Active_Recurring_Definition_Matches`.
- Same-month-previous-year suggestion values are covered with literal expected amounts in `GetMonthPlanPreparationAsync_Should_Suggest_Same_Month_Last_Year_Expenses_Using_Actual_Or_Planned_Basis`.
- Scale rounding is covered by `GetMonthPlanPreparationAsync_Should_Round_Suggested_PlannedAmount_By_Scale`.
- Applying selected suggestions with an edited amount is covered by `ApplyMonthPlanSuggestionsAsync_Should_Create_Target_Month_With_Edited_PlannedAmount`.
- Recurring duplicate suppression is covered by `ApplyMonthPlanSuggestionsAsync_Should_Skip_Recurring_Duplicates_And_Keep_AutoSynced_Expense`.
- Applying older manual history now covered by active recurring sync is guarded by `ApplyMonthPlanSuggestionsAsync_Should_Skip_Manual_History_When_Active_Recurring_Definition_Matches`.
- Explicit target copy and line-item stripping are covered by `CopySelectedExpensesToMonthAsync_Should_Copy_Selected_Items_To_Explicit_Target_And_Strip_LineItems`.
- Loan-backed expense copy suppression is covered by `CopySelectedExpensesToMonthAsync_Should_Skip_LoanBacked_Expenses`.
- Annual plan create/update/user-scope projection is covered by `UpsertAnnualPlanAsync_Should_Create_Update_And_Respect_UserScope`.
- Annual plan non-negative validation is covered by `UpsertAnnualPlanAsync_Should_Reject_Negative_Targets`.
- Alert-prep candidate threshold and no-event side effect are covered by `GetYearStatisticsAsync_Should_Return_DeviationAlertCandidates_Only_Above_Twenty_Percent_And_Without_Publishing_Events`.

## Manual Browser Smoke

These checks are still pending human/browser confirmation.

| Check | Status | Notes |
| --- | --- | --- |
| Missing month with previous-year expenses shows suggestions before month creation; edited price is applied after confirmation. | Pending manual | Use a month that does not yet exist and the same month in the previous year with expenses. |
| Skipping suggestions creates/loads the month with recurring auto-sync and no historical suggestions. | Pending manual | Confirm recurring rows appear and historical suggestions are not inserted. |
| Copy selected expenses to a chosen non-adjacent target month. | Pending manual | Confirm actual amounts, line items, and loan-backed rows are not copied. |
| `Plan roczny` target values persist after reload. | Pending manual | Save expected income/savings, reload Statistics, and confirm the values remain. |
| Alert candidates appear as preparation only, with no sent-notification language. | Pending manual | Use a year with a category above the prior populated-month average by more than 20%. |

## Scope Guardrails

- No `Safe-to-spend` / `SafeToSpend` output was reintroduced.
- Historical suggestions and copy remain expense-only.
- Actual amounts and line items are not copied into target months.
- Recurring expenses and loan installments remain authoritative through their existing sync paths.
- Annual targets stay as year-level income and savings totals only; no monthly grid or category annual budget was added.
