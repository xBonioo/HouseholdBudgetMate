# Frame Brief: Active Accounts in the Balance Editor

> Framing step before implementation. This document separates the observed UI symptom from its confirmed cause.

## Reported Observation

The "Salda kont" section shows no accounts even though eight accounts are active, so no balance amounts can be entered.

## Initial Framing (preserved)

- **User's stated cause or approach**: Active accounts are missing from the balance editor.
- **User's proposed direction**: Allow amounts to be entered for active accounts.
- **Pre-dispatch narrowing**: One specific symptom affecting the account-balance editor.

## Dimension Map

1. **Account loading** - `AccountService.GetAllAsync` could return no accounts.
2. **Selected-month filtering** - the UI could remove loaded active accounts based on historical applicability.
3. **Closed-month behavior** - a closed month intentionally shows only persisted balances and is read-only.
4. **Rendering** - loaded balance rows could fail to render.

## Hypothesis Investigation

| Hypothesis | Evidence | Verdict |
| --- | --- | --- |
| Account loading is empty | The account overview and management list use the same `_accounts` collection and report the active accounts. | NONE |
| Selected-month filtering removes active accounts | `GetAccountsForSelectedMonth` calls `IsApplicableForSelectedMonthBalance`, which checks `ActiveFromUtc` before accepting `!IsArchived`. | STRONG |
| The selected month is closed | Closed months are explicitly read-only by product invariant; this is not the requested editable state. | WEAK |
| Rendering drops rows | The Razor markup renders every item in `_balanceRows` without another account filter. | NONE |

## Narrowing Signals

- The user confirmed this is one specific symptom.
- `_balanceRows` is the only collection used by the balance editor and is produced by the selected-month filter.

## Cross-System Convention

The management surface defines an active account as `!IsArchived`. Historical activation and archive timestamps remain relevant for archived-account applicability and financial calculations, while closed months remain read-only.

## Reframed Problem Statement

> **The actual problem is**: the open-month balance editor applies historical activation filtering before honoring the account's current active status, so active accounts can disappear from an editor labeled and intended for active accounts.

The active status must control inclusion in the editable list. Historical date rules still apply to archived accounts, and closed-month immutability remains unchanged.

## Confidence

- **HIGH** - the exclusion path is isolated in the component and no later rendering filter exists.

## What Changes for Implementation

Make current active status sufficient for inclusion in an open month's balance editor and protect that precedence with a focused UI contract test.

## References

- `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs`
- `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor`
- `src/HouseholdBudgetMate.Tests/Tests/Ui/MonthlyBudgetingLoopUiTests.cs`
