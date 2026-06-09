# Artifact 1: Territory Map

Data: zapytanie objelo ostatnie 12 miesiecy, od 2025-06-09 do 2026-06-09. Efektywna historia projektu w tym repo zaczyna sie 2026-04-02 (`0839200 Initial commit`), wiec wyniki pokazuja aktywnosc od 2026-04-02 do 2026-06-09.

Cel: wskazac realne obszary aktywnosci hands-on w repo Household Budget Mate, odszumic wyniki z plikow generowanych/configowych oraz znalezc sprzezenia miedzy obszarami.

## Metodyka

- Zakres zapytania: `git log --since=2025-06-09`.
- Efektywny zakres danych: 2026-04-02 - 2026-06-09, bo pierwszy commit w repo jest z 2026-04-02.
- Liczenie zmian: wystapienia zmienionych sciezek w commitach, nie liczba linii.
- Wspolwystepowanie: dla kazdego commita liczono unikalne obszary, aby wiele plikow w jednym katalogu nie pompowalo wyniku.
- Po filtrze: 80 commitow, 990 wystapien zmienionych sciezek, 414 unikalnych plikow.
- Commity z co najmniej jednym sensownym obszarem kodu: 65.

## Filtr szumu

Odfiltrowano m.in.:

- `context/`, `docs/`, root README/licencje/notatki.
- Lockfile'y: `package-lock.json`, `yarn.lock`, `pnpm-lock.yaml`.
- Dotenvy, `.gitignore`, katalogi narzedziowe `.github/`, `.codex/`, `.playwright-cli/`.
- `bin/`, `obj/`.
- Pliki generowane: WiX `Installer/Generated`, EF migration designer/snapshot/migrations.
- Minified/vendor JS, obrazy, appsettings/launchSettings.
- Konfiguracje typu `package.json`, `playwright.config.ts`, Docker/render.
- Pliki projektow i rozwiazan: `.csproj`, `.slnx`, `.wixproj`.

## TOP 10 Obszarow

| # | Zmiany | Obszar |
|---:|---:|---|
| 1 | 74 | `Web/Components/Pages/PlanPage` |
| 2 | 37 | `Abstractions/Contracts/Expenses/Dto` |
| 3 | 28 | `Web/Components/Layout` |
| 4 | 28 | `Abstractions/Contracts/Expenses/Requests` |
| 5 | 23 | `Abstractions/Contracts/Categories/Requests` |
| 6 | 22 | `Application/Services/ExpenseService` |
| 7 | 20 | `Web/Program.cs` |
| 8 | 18 | `Tests/Services/ExpenseServiceTests` |
| 9 | 17 | `Web/Components/Dialogs` |
| 10 | 16 | `Abstractions/Contracts/Accounts/Requests` |

## TOP 10 Plikow

| # | Zmiany | Plik |
|---:|---:|---|
| 1 | 22 | `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs` |
| 2 | 20 | `src/HouseholdBudgetMate.Web/Program.cs` |
| 3 | 18 | `src/HouseholdBudgetMate.Web/Components/Layout/MainLayout.razor` |
| 4 | 18 | `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs` |
| 5 | 16 | `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor` |
| 6 | 15 | `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor` |
| 7 | 13 | `src/HouseholdBudgetMate.Abstractions/Interfaces/IExpenseService.cs` |
| 8 | 13 | `src/HouseholdBudgetMate.Web/Components/Pages/Home.razor` |
| 9 | 12 | `src/HouseholdBudgetMate.Application/Services/IncomeService.cs` |
| 10 | 11 | `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor` |

## Nacisk Pracy Tydzien Po Tygodniu

| Tydzien od | Commity | Zmiany | Glowne obszary |
|---|---:|---:|---|
| 2026-03-30 | 6 | 266 | Core app code 59, Categories/tags 50, Expenses 42 |
| 2026-04-06 | 5 | 174 | Loans 35, Expenses 33, Categories/tags 32 |
| 2026-04-13 | 5 | 38 | Expenses 27, Monthly planning/PlanPage 5 |
| 2026-04-20 | 1 | 17 | Monthly planning/PlanPage 11 |
| 2026-05-04 | 1 | 7 | Incomes/live balance 3, Expenses 2 |
| 2026-05-11 | 2 | 18 | Expenses 9, Monthly planning/PlanPage 4, Accounts/balances 3 |
| 2026-05-18 | 6 | 119 | Access/setup/admin safety 29, Monthly planning/PlanPage 17 |
| 2026-05-25 | 21 | 169 | Access/setup/admin safety 55, Loans 21, Monthly planning/PlanPage 19 |
| 2026-06-01 | 26 | 112 | Monthly planning/PlanPage 29, Expenses 13, core app code 22 |
| 2026-06-08 | 7 | 70 | Backup/restore 46, Access/setup/admin safety 5 |

Uwaga: tydzien `2026-03-30` oznacza tydzien kalendarzowy zaczynajacy sie w poniedzialek; realne commity w tym wierszu zaczynaja sie 2026-04-02.

Trend: poczatek historii repo to szeroka praca nad fundamentami, kategoriami i wydatkami; potem mocny blok kredytow; od polowy kwietnia nacisk przeszedl na wydatki i planowanie miesieczne; od 2026-05-18 widac zwrot na access/setup/admin safety; poczatek czerwca wraca do miesiecznego loopa budzetowego; tydzien 2026-06-08 jest zdominowany przez backup/restore.

## Najsilniejsze Sprzezenia Obszarow

### Pary

| Commity | Sprzezenie |
|---:|---|
| 18 | `Application/Services/ExpenseService` + `Web/Components/Pages/PlanPage` |
| 17 | `Application/Services/ExpenseService` + `Tests/Services/ExpenseServiceTests` |
| 14 | `Tests/Services/ExpenseServiceTests` + `Web/Components/Pages/PlanPage` |
| 14 | `Web/Components/Pages/Accounts` + `Web/Components/Pages/PlanPage` |
| 13 | `Abstractions/Interfaces/IExpenseService` + `Application/Services/ExpenseService` |

### Trojki

| Commity | Sprzezenie |
|---:|---|
| 13 | `ExpenseService` + `ExpenseServiceTests` + `PlanPage` |
| 12 | `IExpenseService` + `ExpenseService` + `ExpenseServiceTests` |
| 11 | `Expenses/Dto` + `ExpenseService` + `ExpenseServiceTests` |
| 10 | `Expenses/Dto` + `IExpenseService` + `ExpenseService` |
| 10 | `IExpenseService` + `ExpenseService` + `PlanPage` |

Wnioski:

- `ExpenseService` + `PlanPage` to najmocniejsze sprzezenie. Planowanie miesieczne jest silnie zalezne od semantyki wydatkow.
- `ExpenseService` + `ExpenseServiceTests` wyglada zdrowo: najczesciej ruszany serwis ma regularnie aktualizowane testy.
- `ExpenseServiceTests` + `PlanPage` sugeruje, ze user-facing flow planowania czesto wymusza korekty logiki domenowej wydatkow.

## Pliki-Laczniki

Po filtrze szumu najsilniejsze pliki laczace wiele roznych obszarow to:

| Plik | Commity | Rozne obszary wspolwystepujace |
|---|---:|---:|
| `src/HouseholdBudgetMate.Web/Program.cs` | 20 | 226 |
| `src/HouseholdBudgetMate.Web/Components/Layout/MainLayout.razor` | 18 | 204 |
| `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs` | 22 | 190 |
| `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs` | 10 | 188 |
| `src/HouseholdBudgetMate.Web/Components/Pages/Home.razor` | 13 | 177 |
| `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs` | 18 | 171 |

Nie znaleziono jednego globalnego pliku typu i18n/config, ktory falszowalby cala analize. Najblizej wspolnego mianownika repo sa `Program.cs` i `MainLayout.razor`, czyli kompozycja aplikacji/startup oraz shell/nawigacja. Po lzejszym filtrze wysoko pojawia sie `ApplicationDbContextModelSnapshot.cs`, ale to plik generowany przez EF i powinien pozostac poza interpretacja hands-on.

## Weryfikacja Obecnosci Plikow

Mocno sprzezone pliki, ktore nadal istnieja:

- `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs`
- `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs`
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor`
- `src/HouseholdBudgetMate.Abstractions/Interfaces/IExpenseService.cs`
- `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Dto`
- `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor`
- `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs`
- `src/HouseholdBudgetMate.Web/Components/Layout/MainLayout.razor`
- `src/HouseholdBudgetMate.Web/Program.cs`
- `src/HouseholdBudgetMate.Application/Services/IncomeService.cs`

Historyczne sciezki, ktorych juz nie ma:

- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage.razor`
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage.razor.cs`
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage.razor.css`

Interpretacja: wnioski o obszarze PlanPage sa nadal aktualne, ale nalezy odnosic sie do obecnego katalogu `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/`, nie do starej pojedynczej sciezki pliku.

## Najwazniejszy Obraz Terytorium

Najbardziej aktywnym i sprzezonym terytorium repo jest miesieczny loop budzetowy: `PlanPage`, `ExpenseService`, kontrakty wydatkow oraz testy `ExpenseServiceTests`. Drugim istotnym pasmem sa obszary wspolne UI/startupu: `Program.cs`, `MainLayout.razor`, strony `Accounts`, `Home`, `Statistics`. Trzecim wyraznym epizodem czasowym jest access/setup/admin safety w koncowce maja oraz backup/restore w tygodniu 2026-06-08.
