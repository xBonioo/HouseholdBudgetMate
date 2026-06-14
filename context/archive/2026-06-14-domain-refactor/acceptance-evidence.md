# Domain Refactor Acceptance Evidence

Date: 2026-06-14

## Automated Verification

- `dotnet build HouseholdBudgetMate.slnx -c Release`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~ExpenseServiceTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~IncomeServiceTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~BackupServiceTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests|FullyQualifiedName~MonthlyBudgetingLoopUiTests|FullyQualifiedName~MonthlyBudgetingLoopRenderedTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~ExpenseServiceTests|FullyQualifiedName~IncomeServiceTests|FullyQualifiedName~BackupServiceTests|FullyQualifiedName~AccountServiceTests"`
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --no-build`
- `git diff --check -- .`

## Notes

- Build passed with the existing MudBlazor `MUD0002` analyzer warning in `PlanPage.razor`.
- Backup restore now recalculates restored parent expense actual amounts after restoring line items.
- Live balance now uses the same effective actual policy as the expense projections.
- Plan and Accounts now load the new monthly picture contract where they previously stitched plan and live balance separately.
- Manual verification confirmed by user on 2026-06-14 for the monthly Plan/Accounts scenario, `Live balance`, `Pozostalo w planie`, savings transfer timing, incomplete-balance guidance, closed-month savings-transfer affordances, backup restore line-item recalculation, and evidence review.
