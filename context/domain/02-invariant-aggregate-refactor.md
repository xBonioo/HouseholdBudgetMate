---
title: "Plan refaktoru agregatu-straznika dla niezmiennika wydatku"
created: 2026-06-13
type: refactor-plan
---

# Plan refaktoru agregatu-straznika dla niezmiennika wydatku

## KROK 0 - Odkryty kontekst

### Zrodla wymagan i cel produktu

Najwazniejszy cel produktu nie jest ogolnym CRUD-em budzetowym, tylko wiarygodnym obrazem miesiaca. PRD mowi, ze czlonek gospodarstwa traci czas na skladanie planu, realnych wydatkow, kont, kredytow, cyklicznych platnosci i oszczednosci z roznych miejsc (`context/foundation/prd.md:20`). Produkt ma zrekonsyliowac te elementy w jeden live financial picture (`context/foundation/prd.md:22`). Primary success criteria wymieniaja plan miesiaca, real/unexpected expenses, `Live balance`, remaining plan progress, savings context i incomplete-balance guidance (`context/foundation/prd.md:30`-`context/foundation/prd.md:35`).

Regula biznesowa miesiaca jest jawnie opisana: aplikacja ciagle rekonsyliuje planned expenses, real expenses including detailed line items, unexpected expenses, recurring commitments, incomes, account balances i savings transfers (`context/foundation/prd.md:99`-`context/foundation/prd.md:103`). `Live balance` ma byc current liquidity z poprzednich sald zamkniecia i datowanych ruchow miesiaca, a `Pozostalo w planie` to plan progress from planned versus actual expenses (`context/foundation/prd.md:105`). Jezeli brakuje wymaganych sald poprzedniego miesiaca, `Live balance` musi byc pokazany jako niekompletny, nie jako wiarygodna kwota (`context/foundation/prd.md:107`).

### Stack i warstwy

Stack z README: .NET 10, ASP.NET Core Blazor Server, MudBlazor, EF Core, PostgreSQL/Npgsql, Serilog, xUnit, FluentAssertions, NetArchTest, Docker/Render/WiX (`README.md:7`-`README.md:18`).

Warstwy:

| Warstwa | Lokalizacja | Znaczenie dla reguly |
|---|---|---|
| UI | `src/HouseholdBudgetMate.Web` | Blazor pages/forms. README mowi, ze UI wola serwisy aplikacyjne, nie baze (`README.md:36`-`README.md:40`). |
| Application | `src/HouseholdBudgetMate.Application` | Logika uzycia, walidacja, mapowanie. README mowi, ze logika przypadkow uzycia mieszka w Application (`README.md:39`-`README.md:44`). |
| Domain | `src/HouseholdBudgetMate.Domain` | Encje, konfiguracje EF i bazowe typy (`README.md:25`-`README.md:27`). Obecnie encje maja publiczne settery, wiec nie sa straznikami zachowania. |
| Persistence | `src/HouseholdBudgetMate.Migrations` | `ApplicationDbContext` i migracje EF (`README.md:27`, `README.md:42`). |
| Contracts | `src/HouseholdBudgetMate.Abstractions` | DTO, requesty, interfejsy (`README.md:25`, `README.md:41`). |
| Tests | `src/HouseholdBudgetMate.Tests` | Testy regul uslug, miesiecznej petli, audytu i architektury (`README.md:229`-`README.md:236`). |

## KROK 1 - Zidentyfikowane niezmienniki biznesowe

| ID | Niezmiennik | Zrodlo z dokumentow | Zrodlo z kodu |
|---|---|---|---|
| INV-01 | Jezeli wydatek ma pozycje szczegolowe, jego `ActualAmount` musi rownac sie sumie `ExpenseLineItem.Amount`; jezeli nie ma line items, `ActualAmount` moze byc wpisany recznie. | `domain.md`: line items sa kontrolowane przez kategorie, a `ActualAmount = SUM(ExpenseLineItem.Amount)` gdy istnieja (`context/foundation/domain.md:145`-`context/foundation/domain.md:150`). PRD wlacza detailed line items do rekonsyliacji miesiaca (`context/foundation/prd.md:101`-`context/foundation/prd.md:103`). | Helper liczy sume line items (`src/HouseholdBudgetMate.Application/Helpers/ExpenseActualAmountCalculator.cs:7`-`src/HouseholdBudgetMate.Application/Helpers/ExpenseActualAmountCalculator.cs:13`). Mapping DTO uzywa helpera (`src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs:23`-`src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs:25`). |
| INV-02 | `Live balance` jest liczony z poprzednich sald niesavings + due incomes - actual expenses - due savings transfers; nie jest zapisywany jako kolumna. | Formula i zasady kompletnej bazy sald (`context/foundation/domain.md:113`-`context/foundation/domain.md:135`). | `IncomeService.GetLiveBalanceAsync` liczy `currentBalance = accountBaseTotal + incomesTotal - expensesTotal - savingsTransfersTotal` (`src/HouseholdBudgetMate.Application/Services/IncomeService.cs:475`-`src/HouseholdBudgetMate.Application/Services/IncomeService.cs:493`). |
| INV-03 | Otwarty miesiac wymaga poprzednich sald zamkniecia dla kazdego applicable non-savings account; brak wiersza to brak danych, a 0 jest kompletna wartoscia. | `context/foundation/domain.md:120`-`context/foundation/domain.md:135`; PRD incomplete-balance guidance (`context/foundation/prd.md:105`-`context/foundation/prd.md:107`). | Brak salda trafia do `missingBalanceAccountNames` (`src/HouseholdBudgetMate.Application/Services/IncomeService.cs:457`-`src/HouseholdBudgetMate.Application/Services/IncomeService.cs:465`). UI pokazuje "Brak danych" gdy baza niekompletna (`src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor:84`-`src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor:94`). |
| INV-04 | Zamkniety miesiac jest read-only; edycje wydatkow, przychodow, transferow i sald musza byc zatrzymane. | `domain.md`: closed months are read-only (`context/foundation/domain.md:159`-`context/foundation/domain.md:164`). | `BudgetHelper.EnsureMonthIsOpen` rzuca `BadRequestException` (`src/HouseholdBudgetMate.Application/Helpers/BudgetHelper.cs:14`-`src/HouseholdBudgetMate.Application/Helpers/BudgetHelper.cs:19`); uslugi wywoluja go w edycji expense/line item/savings/balance (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2035`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2044`, `src/HouseholdBudgetMate.Application/Services/AccountService.cs:182`-`src/HouseholdBudgetMate.Application/Services/AccountService.cs:188`). |
| INV-05 | Automatyczne/cykliczne przygotowanie miesiaca nie moze tworzyc duplikatow recurring items. | PRD guardrail: automatic month preparation must not create duplicate recurring items (`context/foundation/prd.md:41`-`context/foundation/prd.md:44`). Domain: generacja idempotentna (`context/foundation/domain.md:152`-`context/foundation/domain.md:157`). | `AddRegularDefinitionToMonthAsync` sprawdza istnienie definicji w miesiacu (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:345`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:357`). Copy/apply pomijaja duplikaty (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1851`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1881`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1951`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:1978`). |
| INV-06 | Dane budzetu nie sa widoczne przed odblokowaniem profilu PIN; PIN ma 4-8 cyfr i jest hashowany. | PRD: member unlocks profile with PIN before budget data (`context/foundation/prd.md:51`-`context/foundation/prd.md:61`); access control (`context/foundation/prd.md:111`-`context/foundation/prd.md:117`). | `PinHasher` waliduje i hashuje PIN (`src/HouseholdBudgetMate.Application/Security/PinHasher.cs:11`-`src/HouseholdBudgetMate.Application/Security/PinHasher.cs:23`, `src/HouseholdBudgetMate.Application/Security/PinHasher.cs:53`-`src/HouseholdBudgetMate.Application/Security/PinHasher.cs:58`). Query filters uzywaja `CurrentBudgetOwnerUserId` albo no-access scope (`src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:13`-`src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:18`, `src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:213`-`src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:230`). |
| INV-07 | Limit koperty nie blokuje zapisu wydatku, ale przekroczenie musi byc sygnalizowane. | `context/foundation/domain.md:137`-`context/foundation/domain.md:143`. | `EmitBudgetExceededEventIfNeededAsync` publikuje `BudgetExceededEvent` tylko przy przejsciu z nieprzekroczonego na przekroczony limit (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2888`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2916`). |
| INV-08 | `AuditLogs` sa historia zmian finansowych i nie sa kasowane przez retencje logow operacyjnych. | README (`README.md:205`-`README.md:213`); domain retention (`context/foundation/domain.md:181`-`context/foundation/domain.md:183`). | Interceptor buduje audit logi (`src/HouseholdBudgetMate.Application/Auditing/AuditSaveChangesInterceptor.cs:60`-`src/HouseholdBudgetMate.Application/Auditing/AuditSaveChangesInterceptor.cs:95`); cleanup usuwa tylko `dbContext.Logs` (`src/HouseholdBudgetMate.Application/Kernel/Logging/OperationalLogCleanupService.cs:51`-`src/HouseholdBudgetMate.Application/Kernel/Logging/OperationalLogCleanupService.cs:58`). |

## KROK 2 - Klasyfikacja i wybor #1

| ID | Rdzeniowosc dla sensu produktu | Rozsmarowanie po warstwach | Status egzekwowania | Ocena |
|---|---|---|---|---|
| INV-01 | Bardzo wysoka. Rzeczywiste wydatki, detailed line items i actual expenses sa wejściem do live month picture (`context/foundation/prd.md:99`-`context/foundation/prd.md:105`). | Wysokie: UI ustawia/disable'uje amount, Application zapisuje i recalc, Mapping liczy DTO, IncomeService czyta persisted value, Tests utrwalaja zachowanie. | Naruszalny. Jest helper i testy, ale brak jednego agregatu; line item amount nie ma walidacji `>= 0` w validatorze (`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:272`-`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:287`, `src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:300`-`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:315`); `Live balance` sumuje zapisana kolumne (`src/HouseholdBudgetMate.Application/Services/IncomeService.cs:480`-`src/HouseholdBudgetMate.Application/Services/IncomeService.cs:483`). | #1 |
| INV-02 | Bardzo wysoka. `Live balance` jest w success criteria i FR-007 (`context/foundation/prd.md:32`-`context/foundation/prd.md:35`, `context/foundation/prd.md:82`-`context/foundation/prd.md:83`). | Srednie: glownie IncomeService + UI + DTO. | Egzekwowany jako projekcja, ale zalezy od INV-01 dla poprawnosci actual expenses. | #2 |
| INV-03 | Wysoka. Warunek zaufania do `Live balance`. | Srednie: IncomeService + Accounts UI. | Czesiowo egzekwowany; UI blokuje prezentacje wiarygodnej kwoty, ale backend zwraca liczbe plus flagi. | #3 |
| INV-04 | Wysoka. Zamkniecie miesiaca jest czescia north star flow. | Srednie-wysokie: helper + wiele serwisow + UI `EnsureMonthEditable`. | Raczej egzekwowany fail-fast w serwisach, ale nie w encjach. | #4 |
| INV-05 | Srednia-wysoka. Guardrail dla preparation, ale nice-to-have/later iteration (`context/foundation/prd.md:36`-`context/foundation/prd.md:44`). | Wysokie: sync, copy, apply. | Egzekwowany aplikacyjnie, brak widocznego constraintu DB. | #5 |
| INV-06 | Wysoka dla bezpieczenstwa, supporting dla core budgeting. | Wysokie: session, service, DbContext filters. | Dobrze egzekwowany. | #6 |
| INV-07 | Srednia. Koperty pomagaja interpretowac wydatki, ale nie sa glowna obietnica. | Srednie. | Czesiowo egzekwowany eventem; zapis nie blokuje zgodnie z modelem. | #7 |
| INV-08 | Srednia. Zaufanie/operacje, nie core budgeting. | Srednie. | Dobrze egzekwowany przez interceptor i cleanup. | #8 |

### Wybor #1

Wybrany invariant: **INV-01 - `Expense.ActualAmount` musi byc wartoscia pochodna od line items, gdy line items istnieja; reczny `ActualAmount` jest legalny tylko przy braku line items.**

Uzasadnienie: to invariant najbardziej jednoczesnie rdzeniowy i naruszalny. Jest rdzeniowy, bo PRD definiuje rzeczywiste wydatki i detailed line items jako wejscia do jednego live month picture (`context/foundation/prd.md:99`-`context/foundation/prd.md:103`), a `Live balance` odejmuje actual expenses (`context/foundation/domain.md:123`-`context/foundation/domain.md:131`). Jest slabo egzekwowany, bo nie ma jednego miejsca zapisu: UI zeruje/disable'uje input, Application recalc'uje po osobnych `SaveChangesAsync`, Mapping liczy DTO w locie, a `IncomeService` bierze persisted `ActualAmount`. Najwieksze ryzyko: plan miesiaca moze byc wyswietlany z poprawna suma line items przez mapping, ale `Live balance` moze bazowac na niespojnym zapisanym `ActualAmount`, jezeli jakas sciezka zapisu ominie recalc.

## KROK 3 - Diagnoza wybranego niezmiennika

### Gdzie dzis zyje regula

| Warstwa | Obecne miejsce | Dowod | Diagnoza |
|---|---|---|---|
| Dokumenty | Domain rules: line items | `ActualAmount = SUM(ExpenseLineItem.Amount)` gdy kategoria pozwala na line items (`context/foundation/domain.md:145`-`context/foundation/domain.md:150`). | Regula jest opisana jako invariant wydatku, ale nie ma odpowiadajacego agregatu. |
| Contracts | `CreateExpenseRequest` / `UpdateExpenseRequest` | Oba requesty przyjmuja `ActualAmount` (`src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Requests/CreateExpenseRequest.cs:5`-`src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Requests/CreateExpenseRequest.cs:12`, `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Requests/UpdateExpenseRequest.cs:5`-`src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Requests/UpdateExpenseRequest.cs:11`). | Klient nadal moze przeslac actual amount nawet wtedy, gdy wydatek ma line items. Serwer czesciowo ignoruje te wartosc, ale kontrakt nie komunikuje tego jako invariant. |
| Contracts | `CreateExpenseLineItemRequest` / `UpdateExpenseLineItemRequest` | Requesty maja `Amount` (`src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Requests/CreateExpenseLineItemRequest.cs:5`-`src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Requests/CreateExpenseLineItemRequest.cs:9`, `src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Requests/UpdateExpenseLineItemRequest.cs:5`-`src/HouseholdBudgetMate.Abstractions/Contracts/Expenses/Requests/UpdateExpenseLineItemRequest.cs:9`). | Kwota line item jest wejściem do invariant, ale sama nie ma widocznej walidacji nieujemnosci w validatorze. |
| Validation | Expense amount validators | `CreateExpenseValidator` i `UpdateExpenseValidator` wymagaja `PlannedAmount >= 0` i `ActualAmount >= 0` (`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:75`-`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:81`, `src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:115`-`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:121`). | Reczny actual amount ma walidacje. |
| Validation | Line item validators | `CreateExpenseLineItemRequestValidator` waliduje `ExpenseId`, `Description`, `TagId`, ale nie `Amount` (`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:272`-`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:287`). `UpdateExpenseLineItemRequestValidator` tak samo (`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:300`-`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:315`). | Brak fail-fast dla ujemnej kwoty line item po stronie serwera. UI parsuje kwote, ale to nie jest domenowy straznik. |
| UI | Create expense | UI parsuje non-negative amount i ustawia `ActualAmount = 0`, jezeli wybrana kategoria/tag wspiera line items (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:102`-`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:123`). | UI jest czesciowym straznikiem. To pomaga UX, ale invariant nie moze na tym polegac. |
| UI | Edit expense | Przy edycji UI liczy sume line items i disable'uje actual amount, gdy sa line items albo selection wspiera line items (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:145`-`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:168`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:712`-`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:784`). | UI ukrywa nielegalna edycje, ale nie zatrzymuje zewnetrznego/alternatywnego klienta. |
| UI | Create/update line item | UI parsuje line item amount przez `TryParseAmountOrWarn` i wysyla request do serwisu (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:36`-`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:65`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:98`-`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:116`). | UI jest jedynym widocznym miejscem filtrowania kwoty line item. To jest slabosc. |
| Application | Create line item | Serwis laduje expense z category/tag/month/line items, sprawdza open month i line-item support, dodaje line item, zapisuje, przelicza actual amount, zapisuje drugi raz (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2026`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2091`). | Regula jest egzekwowana proceduralnie, ale w dwoch save'ach. Gdy pierwszy save przejdzie, a drugi padnie, line item moze zostac zapisany bez aktualizacji parent amount. |
| Application | Update line item | Serwis laduje line item z expense/category/tag/month, sprawdza open month i support (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2118`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2168`). Dalej aktualizuje i recalc'uje rodzica w osobnych operacjach zapisu (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2176`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2178`). | Ten sam problem atomowosci; dodatkowo brak walidacji amount w validatorze. |
| Application | Delete line item | Usuwa line item, zapisuje, przelicza rodzica i zapisuje drugi raz (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2208`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2225`). | Dwa save'y. Dodatkowo final-line-item behavior zachowuje ostatni actual amount, co jest swiadoma decyzja w testach, ale koliduje z prostym odczytem "gdy brak line items, reczne actual amount". |
| Application | Update expense | Gdy istnieja line items, request `ActualAmount` jest ignorowany, a po zapisie jest recalc (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2228`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2281`). | Lepiej niz UI-only, ale nadal proceduralne i w dwoch zapisach. |
| Application helper | Calculation | `ExpenseActualAmountCalculator.GetEffectiveActualAmount` zwraca sume line items, jezeli `LineItems.Count > 0`, inaczej zapisany `ActualAmount` (`src/HouseholdBudgetMate.Application/Helpers/ExpenseActualAmountCalculator.cs:7`-`src/HouseholdBudgetMate.Application/Helpers/ExpenseActualAmountCalculator.cs:13`). | Kalkulator jest pasywny. Nie zatrzymuje nielegalnego stanu i nie zapewnia atomowosci. |
| Mapping/read model | Expense DTO | Mapping ustawia DTO `ActualAmount` z helpera (`src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs:23`-`src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs:25`). | Odczyt planu moze maskowac niespojna kolumne w bazie, bo DTO liczy wartosc z line items. |
| Monthly KPI | Month plan KPI | KPI uzywa `expenses.Sum(x => x.ActualAmount)` z DTO (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2495`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2513`). | Plan/KPI uzywaja wartosci wyliczonej przez DTO, niekoniecznie tej samej co `Live balance`. |
| Live balance | IncomeService | `expensesTotal` sumuje `dbContext.Expenses.SumAsync(x => x.ActualAmount)` (`src/HouseholdBudgetMate.Application/Services/IncomeService.cs:475`-`src/HouseholdBudgetMate.Application/Services/IncomeService.cs:493`). | To najwazniejsze miejsce ryzyka: core projection czyta persisted parent amount, a nie wyliczenie z line items. |
| Tests | Current positive cases | Testy potwierdzaja ignorowanie request `ActualAmount`, recalc po create/update/delete line item, oraz zachowanie ostatniej kwoty po usunieciu ostatniego line item (`src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:3260`-`src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:3328`, `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:3346`-`src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:3399`, `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:3465`-`src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:3671`). | Testy utrwalaja obecny proceduralny model, ale nie wymuszaja atomowosci ani fail-fast dla ujemnych line items. |

### Warstwy, ktore nie egzekwuja lub egzekwuja niespojnie

- Domain: encje `Expense` i `ExpenseLineItem` maja publiczne settery i nie chronia invariantu; nie ma metody domenowej typu `AddLineItem`, `ChangeManualActualAmount`, `RemoveLineItem`.
- Validation: waliduje `Expense.ActualAmount >= 0`, ale nie waliduje `ExpenseLineItem.Amount` w pokazanych liniach.
- Application: egzekwuje recalc po operacjach, ale przez helper i sekwencje `SaveChangesAsync`, nie przez atomowy agregat.
- UI: disable'uje input i zeruje actual amount dla line-item-capable selections, ale to jest klient, nie straznik.
- Read models: `GetMonthAsync`/mapping liczy actual amount z line items, a `GetLiveBalanceAsync` czyta zapisana kolumne. To daje dwa zrodla prawdy.
- "Polykany" blad: `RecalculateActualAmountAsync` po usunieciu ostatniej pozycji robi `return`, gdy `LineItems.Count == 0` (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2734`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2750`). Test opisuje to jako zachowanie celowe (`src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:3618`-`src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:3671`). To nie jest wyjatek, ale jest "cicha decyzja stanu"; plan refaktoru musi nazwac ja jawnie.

## KROK 4 - Projekt agregatu-straznika

### Agregat root

Proponowany root: `TrackedExpense`.

Powod nazwy: nie chodzi o ogolny wydatek jako rekord EF, tylko o wydatek, ktory pilnuje zasad recorded spending. Root obejmuje:

- `ExpenseId`
- `MonthPlanId`
- `MonthStatus`
- `CategoryLineItemPolicy`
- `MainTagLineItemPolicy`
- `PlannedAmount`
- `ActualAmount`
- `LineItems`
- `ShowRemainingInUI`

Wariant docelowy: `Expense` moze pozostac persistence entity, ale zachowanie przeniesc do domenowego modelu/agregatu ladowanego przez repozytorium. Alternatywnie pozniej encja EF moze zostac przeksztalcona w agregat, ale plan minimalizuje blast radius.

### Nazwane bledy domenowe

```csharp
abstract class DomainException : Exception;

sealed class ClosedMonthCannotBeEdited : DomainException;
sealed class LineItemsNotAllowedForExpense : DomainException;
sealed class LineItemAmountMustBePositive : DomainException;
sealed class ManualActualAmountNotAllowedWhenLineItemsExist : DomainException;
sealed class ExpenseCategoryTagMismatch : DomainException;
sealed class LineItemTagMustBelongToExpenseCategory : DomainException;
sealed class LineItemTagMustBeChildOfMainExpenseTag : DomainException;
```

Uwaga: status "positive" dla line item amount powinien byc decyzja domenowa. Dokument `domain.md` nie mowi wprost, czy line item moze byc 0, ale expense validators wymagaja expense amounts `>= 0` (`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:75`-`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:81`). Plan proponuje `> 0` dla line item, bo line item reprezentuje realny skladnik wydatku; jesli zespol chce dopuscic 0, nazwa bledu i test powinny zmienic sie na `LineItemAmountMustBeNonNegative`.

### Metody domenowe i preconditions

```csharp
sealed class TrackedExpense
{
    Money ActualAmount { get; private set; }
    IReadOnlyCollection<ExpenseLineItem> LineItems => _lineItems;

    void ChangeManualAmounts(Money plannedAmount, Money actualAmount, bool showRemaining)
    {
        EnsureOpenMonth();
        EnsureNonNegative(plannedAmount);
        EnsureNonNegative(actualAmount);

        if (_lineItems.Count > 0)
            throw new ManualActualAmountNotAllowedWhenLineItemsExist(Id);

        PlannedAmount = plannedAmount;
        ActualAmount = actualAmount;
        ShowRemainingInUI = showRemaining;
    }

    void ChangePlanningFields(Money plannedAmount, bool showRemaining)
    {
        EnsureOpenMonth();
        EnsureNonNegative(plannedAmount);

        PlannedAmount = plannedAmount;
        ShowRemainingInUI = showRemaining;
        RecalculateActualFromLineItemsIfPresent();
    }

    ExpenseLineItem AddLineItem(LineItemDraft draft, TagSnapshot? tag)
    {
        EnsureOpenMonth();
        EnsureLineItemsAllowed();
        EnsureLineItemAmountValid(draft.Amount);
        EnsureLineItemTagValid(tag);

        var item = ExpenseLineItem.Create(draft.Description, draft.Amount, draft.OccurredAt, draft.TagId);
        _lineItems.Add(item);
        RecalculateActualFromLineItemsRequired();
        return item;
    }

    void ChangeLineItem(LineItemId id, LineItemDraft draft, TagSnapshot? tag)
    {
        EnsureOpenMonth();
        EnsureLineItemsAllowed();
        EnsureLineItemAmountValid(draft.Amount);
        EnsureLineItemTagValid(tag);

        var item = FindLineItemOrThrow(id);
        item.Change(draft.Description, draft.Amount, draft.OccurredAt, draft.TagId);
        RecalculateActualFromLineItemsRequired();
    }

    void RemoveLineItem(LineItemId id, EmptyLineItemsPolicy policy)
    {
        EnsureOpenMonth();
        var removed = RemoveLineItemOrThrow(id);

        if (_lineItems.Count > 0)
        {
            RecalculateActualFromLineItemsRequired();
            return;
        }

        if (policy == EmptyLineItemsPolicy.ResetActualToZero)
            ActualAmount = Money.Zero;
        else if (policy == EmptyLineItemsPolicy.KeepLastActualAsManual)
            MarkActualAmountAsManualAfterLineItems(removed.Amount);
        else
            throw new InvalidEmptyLineItemsPolicy(Id);
    }

    private void RecalculateActualFromLineItemsRequired()
    {
        if (_lineItems.Count == 0)
            throw new DomainInvariantViolation("Cannot recalculate line-item total without line items.");

        ActualAmount = _lineItems.Sum(x => x.Amount);
    }
}
```

### Repozytorium agregatu

```csharp
interface ITrackedExpenseRepository
{
    Task<TrackedExpense> LoadForUpdateAsync(int expenseId, CancellationToken ct);
    Task SaveAsync(TrackedExpense aggregate, CancellationToken ct);
}
```

`LoadForUpdateAsync` musi ladowac naraz:

- `Expense`
- `MonthPlan`
- `Category`
- `Tag`
- `ExpenseLineItems`
- line item tags potrzebne do walidacji

Docelowa zasada: serwis aplikacyjny nie robi recalc i nie ustawia `Expense.ActualAmount` bezposrednio. Wszystkie sciezki `CreateExpenseLineItemAsync`, `UpdateExpenseLineItemAsync`, `DeleteExpenseLineItemAsync`, `UpdateExpenseAsync` przechodza przez `TrackedExpense`.

### Atomowosc

Operacje line-item + parent actual amount musza isc w jednej transakcji i jednym unit of work. Obecnie `CreateExpenseLineItemAsync` zapisuje line item, potem recalc, potem drugi save (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2085`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2091`). Analogiczny problem wystepuje przy delete (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2220`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2225`) i update expense (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2278`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2281`).

Pseudokod:

```csharp
public async Task<ExpenseLineItemDto> CreateExpenseLineItemAsync(CreateExpenseLineItemRequest request, CancellationToken ct)
{
    var command = ParseAndValidateShape(request); // syntactic only

    await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
    await using var tx = await dbContext.Database.BeginTransactionAsync(ct);

    var repository = new EfTrackedExpenseRepository(dbContext);
    var aggregate = await repository.LoadForUpdateAsync(command.ExpenseId, ct);
    var created = aggregate.AddLineItem(command.ToDraft(), await repository.LoadTagAsync(command.TagId, ct));

    await repository.SaveAsync(aggregate, ct); // one SaveChangesAsync
    await tx.CommitAsync(ct);

    return mapper.ToDto(created);
}
```

Jesli provider wymaga execution strategy, uzyc wzorca jak w `BackupService`, gdzie strategia tworzy wykonanie, otwiera transakcje, zapisuje i commit'uje (`src/HouseholdBudgetMate.Application/Services/BackupService.cs:175`-`src/HouseholdBudgetMate.Application/Services/BackupService.cs:181`).

### Cienka trasa / cienki klient

Projekt nie ma publicznego Web API w MVP (`context/foundation/prd.md:119`-`context/foundation/prd.md:123`), wiec "route/API" oznacza tutaj Blazor handler + application service boundary.

Before:

```csharp
// UI decyduje, czy actual amount input ma byc disabled i czy ustawic 0.
_editExpense.ActualAmount = SupportsLineItemsForSelection(...) ? 0 : actualAmount;
await ExpenseService.UpdateExpenseAsync(_editExpense, ct);
```

After:

```csharp
// UI tylko parsuje wejscie i wysyla intencje.
await ExpenseService.ChangeExpensePlanningFieldsAsync(new ChangeExpensePlanningFieldsRequest(...), ct);

// Application service:
try
{
    aggregate.ChangePlanningFields(...);
    await repository.SaveAsync(aggregate, ct);
}
catch (DomainException ex)
{
    throw DomainErrorMapper.ToBadRequest(ex);
}
```

UI moze nadal disable'owac pola dla ergonomii, ale serwer jest jedynym zrodlem prawdy. Nielegalna operacja rzuca nazwany blad domenowy i zatrzymuje zapis.

## KROK 5 - Before/after, plan i testy

### Before/after dzisiejszych miejsc reguly

| Miejsce dzis | Before | After |
|---|---|---|
| UI create/edit expense | UI zeruje `ActualAmount` dla line-item-capable selection (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:115`-`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:118`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:199`-`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Expenses.cs:202`). | UI wysyla intencje. Agregat decyduje, czy manual actual amount jest legalny. |
| UI line item amount | UI parsuje amount przed wyslaniem (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:50`-`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:55`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:105`-`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.LineItems.cs:110`). | UI zostaje pomocniczy; agregat waliduje amount fail-fast. |
| Validators | Line item validators nie waliduja `Amount` (`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:272`-`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:287`, `src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:300`-`src/HouseholdBudgetMate.Application/Validation/Expenses/ExpenseRequestValidators.cs:315`). | Walidacja syntaktyczna moze dodac amount, ale domena nadal rzuca `LineItemAmountMustBePositive` jako ostateczny straznik. |
| `CreateExpenseLineItemAsync` | Add line item -> save -> recalc -> save (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2076`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2091`). | Load aggregate -> `AddLineItem` -> one save/transaction. |
| `UpdateExpenseLineItemAsync` | Update line item -> save -> recalc -> save (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2118`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2178`). | Load aggregate -> `ChangeLineItem` -> one save/transaction. |
| `DeleteExpenseLineItemAsync` | Remove line item -> save -> recalc -> save; final line item keeps last amount (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2208`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2225`, `src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:3618`-`src/HouseholdBudgetMate.Tests/Tests/Services/ExpenseServiceTests.cs:3671`). | Load aggregate -> `RemoveLineItem(policy)` -> one save/transaction; empty-line-items policy jawnie nazwana. |
| `UpdateExpenseAsync` | Ignoruje request actual amount, gdy line items istnieja; potem recalc (`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2267`-`src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:2281`). | `ChangeManualAmounts` rzuca `ManualActualAmountNotAllowedWhenLineItemsExist`; `ChangePlanningFields` nie przyjmuje manual actual amount. |
| Mapping | DTO liczy effective actual amount w locie (`src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs:23`-`src/HouseholdBudgetMate.Application/Mapping/ExpenseExtensionMapping.cs:25`). | Mapping moze nadal byc defensywne, ale persisted `ActualAmount` jest utrzymywany przez agregat. |
| `GetLiveBalanceAsync` | Sumuje persisted `Expense.ActualAmount` (`src/HouseholdBudgetMate.Application/Services/IncomeService.cs:480`-`src/HouseholdBudgetMate.Application/Services/IncomeService.cs:483`). | Po wprowadzeniu agregatu persisted amount staje sie zaufanym read model. Opcjonalnie dodac smoke assertion/test, ze live balance uzywa poprawnego totalu po line item operations. |

### Plan faz refaktoru

1. **Test-first: nazwac invariant na testach domenowych.**
   - Dodac testy dla przyszlego `TrackedExpense`: add line item przelicza actual amount, update line item przelicza, delete one-of-many przelicza, manual actual amount with existing line items rzuca named domain error, negative line item amount rzuca named domain error, closed month rzuca named domain error.
   - Te testy powinny byc bez EF albo z minimalnym builderem agregatu.

2. **Test-first: testy aplikacyjne atomowosci i projekcji.**
   - `CreateExpenseLineItemAsync_Should_Save_LineItem_And_ParentActual_In_One_Unit`.
   - `UpdateExpenseLineItemAsync_Should_Reject_Negative_Amount`.
   - `CreateExpenseLineItemAsync_Should_Reject_Negative_Amount`.
   - `LiveBalance_Should_Use_Recalculated_LineItem_Total_After_Create_Update_Delete`.
   - `UpdateExpenseAsync_Should_Fail_When_ManualActualAmount_Is_Provided_For_LineItemExpense` albo, jezeli kontrakt zostaje kompatybilny, `Should_Ignore_But_Domain_Method_Not_Expose_ManualActual`.

3. **Wprowadzic model domenowy i bledy bez przepinania produkcji.**
   - Dodac `TrackedExpense`, value object `Money`, `LineItemDraft`, named domain exceptions.
   - Zachowac persistence entities bez zmian publicznego kontraktu na tym etapie.

4. **Wprowadzic `ITrackedExpenseRepository` i adapter EF.**
   - Load aggregate z Expense + MonthPlan + Category + Tag + LineItems.
   - Save aggregate mapuje zmiany na EF tracked entities.
   - Dodac transakcje dla line item commands.

5. **Przepiac write paths.**
   - `CreateExpenseLineItemAsync`, `UpdateExpenseLineItemAsync`, `DeleteExpenseLineItemAsync`, `UpdateExpenseAsync` przechodza przez agregat.
   - Usunac bezposrednie recalc z tych sciezek.
   - Zostawic `ExpenseActualAmountCalculator` tylko jako read-model defensive helper albo oznaczyc do usuniecia w kolejnej fazie.

6. **Ujednolic read-side.**
   - Po przepieciu write-side `GetLiveBalanceAsync` moze nadal sumowac persisted `ActualAmount`, bo agregat gwarantuje jego poprawnosc.
   - Dla ostroznosci dodac test cross-service: Plan KPI i Live balance widza ten sam spent total po line item edits.

7. **Porzadkowanie kontraktow i UI.**
   - Rozdzielic requesty: `ChangeExpensePlanningFieldsRequest` bez `ActualAmount` oraz `ChangeExpenseManualActualAmountRequest` tylko dla expenses bez line items.
   - UI nadal disable'uje pola, ale obsluguje nazwane domain errors jako komunikaty.

### Przypadki testowe invariantu

Legalne:

- Utworzenie line item dla otwartego wydatku z category/tag supporting line items ustawia parent `ActualAmount` na kwote itemu.
- Dodanie drugiego line item ustawia parent `ActualAmount` na sume obu.
- Zmiana kwoty line item aktualizuje parent `ActualAmount`.
- Usuniecie jednego z kilku line items aktualizuje parent `ActualAmount` do sumy pozostalych.
- Edycja planned amount przy istniejacych line items nie zmienia manualnie actual amount.
- Wydatek bez line items pozwala ustawic manual `ActualAmount`.

Nielegalne:

- Ujemna kwota line item rzuca `LineItemAmountMustBePositive`.
- Dodanie line item do kategorii/tagu bez supportu rzuca `LineItemsNotAllowedForExpense`.
- Edycja manual `ActualAmount` dla expense z line items rzuca `ManualActualAmountNotAllowedWhenLineItemsExist`.
- Dodanie/edycja/usuniecie line item w zamknietym miesiacu rzuca `ClosedMonthCannotBeEdited`.
- Tag line item spoza kategorii rzuca `LineItemTagMustBelongToExpenseCategory`.
- Tag line item niebedacy child tagiem glownego tagu rzuca `LineItemTagMustBeChildOfMainExpenseTag`.
- Symulowana awaria po dodaniu line item nie zostawia parent `ActualAmount` bez aktualizacji, bo operacja jest transakcyjna.

### Load-bearing nazwy do zarejestrowania

Nie znalazlem osobnego rejestru kontraktow domenowych poza dokumentami `context/` i testami. Nazwy do utrwalenia w `context/domain` oraz w nazwach testow:

- `TrackedExpense`
- `ExpenseActualAmountInvariant`
- `LineItemDraft`
- `ManualActualAmount`
- `LineItemActualAmount`
- `ClosedMonthCannotBeEdited`
- `LineItemsNotAllowedForExpense`
- `LineItemAmountMustBePositive`
- `ManualActualAmountNotAllowedWhenLineItemsExist`
- `EmptyLineItemsPolicy`
- `ITrackedExpenseRepository`
