# Refactor Opportunities Acceptance Evidence

Date: 2026-06-13

## Automated Verification

Implementation-review verification on 2026-06-13:

- PASS: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~AuditTrailTests|FullyQualifiedName~UserScopingTests"` - 25 passed, 0 failed.
- PASS: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~MonthlyBudgetingLoopRenderedTests|FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~IncomeServiceTests"` - 131 passed, 0 failed.
- PASS: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release` - 401 passed, 0 failed.
- PASS: `dotnet build HouseholdBudgetMate.slnx -c Release` - 0 errors; existing `MUD0002` warning in `PlanPage.razor`.
- PASS: `git diff --check -- .` - no whitespace errors; LF/CRLF warnings only.

Additional verification after F2 review fix on 2026-06-13:

- PASS: `dotnet build HouseholdBudgetMate.slnx -c Release` - 0 errors; existing `MUD0002` warning in `PlanPage.razor`.
- PASS: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~ExpenseServiceTests"` - 73 passed, 0 failed.

## Browser Smoke

Live app endpoint used for smoke: `https://localhost:5001/`

Login flow:

- Selected `Kamil (administrator)` from the login dialog.
- PIN used: configured local test PIN, value intentionally not recorded.

### Passed

- Expense create/edit/delete on the monthly plan.
- Line-item create/edit/delete on an existing line-item-capable expense, with the row re-expanding after refresh.
- Income create/edit/delete.
- Savings transfer create/edit/delete.
- Target-month copy into the next month.

### Not Covered

- Month-preparation suggestion apply/skip with `bypassPreparation`: not covered. The suggestion panel did not surface on the seeded months reachable in this environment, so `Pomin propozycje` / `Utworz miesiac z wybranych` remained unavailable to smoke.

## Notes

- Manual progress item 5.6 remains pending until suggestion apply/skip is either smoke-tested with suitable data or intentionally accepted as not covered.
- The completed browser smoke covers the refactored save/reload path on the monthly expense table, line-item child row path, income save family, savings transfer save family, and target-month copy no-source-month-edit path.
