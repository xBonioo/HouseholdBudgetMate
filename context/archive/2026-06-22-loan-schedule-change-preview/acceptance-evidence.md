# Loan Schedule Change Preview Acceptance Evidence

Change: `loan-schedule-change-preview`
Status: in progress
Updated: 2026-06-22

## Automated Verification

- `dotnet test HouseholdBudgetMate.slnx -c Release`
  - Result: passed
  - Summary: 433 tests passed, 0 failed, 0 skipped
- `dotnet build HouseholdBudgetMate.slnx -c Release`
  - Result: passed
- `dotnet build HouseholdBudgetMate.slnx`
  - Result: passed
- `dotnet test src/HouseholdBudgetMate.Tests/HouseholdBudgetMate.Tests.csproj --filter "FullyQualifiedName~LoanUiRedesignTests|FullyQualifiedName~LoanScheduleChangePreviewUiTests"`
  - Result: passed

## Regression Notes

- Existing financial golden assertions in `LoanServiceTests` were left unchanged.
- Preview operations remain side-effect free and compare equal with confirmed persistence for WIBOR, prepayment, and bank-driven installment changes.
- Release test coverage still passes across the full solution, including the loan service regression suite.
- Scope note: prepayment confirmation is part of this PR's main flow. The persisted `LoanPrepayments`
  history is included so confirmed prepayments can be replayed for month-specific debt summaries instead
  of being inferred from display-name expense rows.
- Migration coverage verifies that legacy prepayment backfill imports only unambiguous expense rows
  matched by owner, loan name, tag and positive actual amount.
- Opłacone raty są ukrywane w podglądzie harmonogramu, żeby nie pokazywać bezsensownych wierszy bez zmian.
- Preview i zapis korzystają z tych samych projekcji harmonogramu dla WIBOR, nadpłaty i zmiany raty z banku.
- Testy komponentowe potwierdzają kolejność preview przed zapisem, zachowanie danych przy powrocie do edycji i obsługę nieaktualnej wersji.

## Manual Verification

Pending human walkthrough:

- Open a representative long mortgage schedule and verify the year-grouped preview dialog stays usable.
- Walk through WIBOR, prepayment, and bank installment change flows with preview, back-to-edit, and confirm.
- Verify the schedule shown after confirmation matches the reviewed preview.
- Confirm the accepted mortgage scenario still shows the expected 800000 PLN baseline before the June prepayment path.

## Notes

- No financial calculation algorithm changes were introduced in this change.
- The change includes one schema migration for loan prepayment history. Rollback drops only the
  `LoanPrepayments` table; existing expense rows remain intact.
- The remaining manual items should be completed before the change is marked fully implemented and archived.
