# Verify Monthly Budgeting Loop Acceptance Evidence

> Change: `verify-monthly-safe-to-spend-loop`
> Scope: S-02 controlled monthly budgeting loop
> Status: accepted for controlled monthly-loop MVP scope; real-data gate remains external

## Readiness Prerequisite Status

S-02 is gated by `context/changes/secure-real-data-readiness/readiness-evidence.md`.

Current status on 2026-05-30:

- Backup before first real data: pending in F-02 evidence; current workspace has no `RENDER_EXTERNAL_DATABASE_URL` and no `pg_dump` command.
- Restore smoke test: pending in F-02 evidence; current workspace has no `NON_PRODUCTION_DATABASE_URL` and no `pg_restore` command.
- Live `/health/ready` check: pending manual in F-02 evidence; current workspace has no live service URL.
- Render Blueprint validation/workspace check: pending CLI/manual in F-02 evidence; `render` CLI is not installed in the current shell.
- Admin readiness panel visual review: pending manual in F-02 evidence.
- Final real-data MVP pilot sign-off: pending in F-02 evidence.

Decision for S-02:

- S-02 readiness was explicitly approved by the user for this controlled S-02 run on 2026-05-29.
- The S-02 acceptance scenario is controlled demo data, not real household data.
- The user explicitly rejected a separate safe-to-spend value on 2026-05-29 and reaffirmed on 2026-05-30 that it will not be in the application; phase 2 code/UI/test changes for that value were rolled back.

## Accepted Scope Baseline

S-02 now verifies the monthly budgeting loop with the current accepted financial result model:

- `LiveBalanceDto` exposes live-balance components and balance-base completeness.
- `IncomeService.GetLiveBalanceAsync` calculates `CurrentBalance`, due income, actual expenses, due savings transfers, and completeness.
- `PlanPage`, `Dashboard`, and `Accounts` show live balance, plan/account values, savings context, and incomplete-balance guidance.
- No separate safe-to-spend field, reserve field, or UI KPI is part of acceptance.

## Controlled Scenario Inputs

| Input | Value | Notes |
| --- | --- | --- |
| Household profile | `visible-admin`, PIN `2468` | PIN-protected visible user, shared budget owner `default-user` |
| Year/month | 2026-04 | Controlled open month, test today is 2026-04-10 |
| Previous closing balance | 3000.00 PLN | Non-savings bank account closing balance for 2026-03 |
| Due income | 5000.00 PLN | Expected on 2026-04-05, counted in live balance |
| Planned expense | 1200.00 PLN, later 1300.00 PLN | Positive planned amount |
| Real spending against planned expense | 450.00 PLN, later 500.00 PLN | Actual amount on planned row |
| Unexpected expense | 125.00 PLN | Planned amount 0.00 PLN |
| Future savings transfer | 600.00 PLN on 2026-04-20 | Visible in plan; not deducted from live balance before due date |
| Due savings transfer | 300.00 PLN on 2026-04-10 | Deducted from live balance |

## Expected Formula Breakdown

| Step | Live balance | Plan remaining | Due savings deducted | Future savings pending | Notes |
| --- | ---: | ---: | ---: | ---: | --- |
| Initial controlled state | 8000.00 | 0.00 | 0.00 | 0.00 | 3000 base + 5000 due income |
| After planned expense | 8000.00 | 1200.00 | 0.00 | 0.00 | Planned expense has no actual spend yet |
| After real spend | 7550.00 | 750.00 | 0.00 | 0.00 | Actual planned spend is 450 |
| After unexpected expense | 7425.00 | 750.00 | 0.00 | 0.00 | Unexpected actual spend adds 125 but does not reduce plan remaining |
| After future savings transfer | 7425.00 | 750.00 | 0.00 | 600.00 | Future transfer remains visible but not deducted |
| After due savings transfer | 7125.00 | 750.00 | 300.00 | 600.00 | Due transfer is deducted from live balance |
| After close/reopen/edit/close | 7075.00 | 800.00 | 300.00 | 600.00 | Planned row updated to planned 1300 / actual 500 and month closed again |

## Automated Verification Results

| Phase | Command | Result | Date |
| --- | --- | --- | --- |
| 1 | `Test-Path context/changes/verify-monthly-safe-to-spend-loop/acceptance-evidence.md; Test-Path context/changes/secure-real-data-readiness/readiness-evidence.md` | Passed: both returned `True` | 2026-05-29 |
| 1 | `dotnet build HouseholdBudgetMate.slnx -c Release` | Passed: 0 warnings, 0 errors | 2026-05-29 |
| 2 rollback | `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~IncomeServiceTests"` | Passed after rollback: 45 passed, 0 failed, 0 skipped | 2026-05-29 |
| 2 rollback | `dotnet build HouseholdBudgetMate.slnx -c Release` | Passed after rollback: 0 warnings, 0 errors | 2026-05-29 |
| 3 | `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopTests"` | Passed: 3 passed, 0 failed, 0 skipped | 2026-05-29 |
| 3 | `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~IncomeServiceTests\|FullyQualifiedName~ExpenseServiceTests\|FullyQualifiedName~UserSession\|FullyQualifiedName~UserScoping"` | Passed: 112 passed, 0 failed, 0 skipped | 2026-05-29 |
| 4 | `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"` | Passed: 3 passed, 0 failed, 0 skipped | 2026-05-30 |
| 4/5 | `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release` | Passed: 306 passed, 0 failed, 0 skipped | 2026-05-30 |
| 5 | `dotnet build HouseholdBudgetMate.slnx -c Release` | Passed: 0 warnings, 0 errors | 2026-05-30 |
| 5 | `git diff --check -- .` | Passed: exit code 0 | 2026-05-30 |

## Browser or Component Evidence

- Harness: xUnit component/UI contract tests in `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`.
- Command: `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj -c Release --filter "FullyQualifiedName~MonthlyBudgetingLoopUiTests"`
- Result: Passed, 3/3 tests.
- Screenshot/log artifact: not produced by this lightweight contract harness.
- Notes: This verifies the primary Razor surfaces expose `Live balance`, `Pozostało w planie`, savings context, incomplete-balance guidance, month close/open controls, and do not reintroduce `Safe-to-spend`/`SafeToSpend`. It is not a full browser-clicking test; the user provided final clicked-flow/product sign-off on 2026-05-30.

## Close/Reopen Observations

- Closed-month edit blocking: `UpdateExpenseAsync` throws `BadRequestException` after `CloseMonthAsync`.
- Reopen behavior: `OpenMonthAsync` changes the month back to open and permits the planned/actual expense edit.
- Post-edit live-balance or plan-KPI update: final live balance is 7075.00 PLN and plan remaining is 800.00 PLN.
- Final close behavior: month is closed again after the edit and final values remain readable.

## Final Sign-Off

- S-02 monthly budgeting loop accepted: Yes, for controlled MVP monthly-loop scope.
- Approved by: User confirmation in thread ("Potwierdzam").
- Date: 2026-05-30
- Conditions or follow-up work: F-02 real-data evidence remains pending before real household data entry: `pg_dump`, restore smoke test, live `/health/ready`, Render workspace/blueprint check, and admin readiness panel review.
