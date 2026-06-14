# Sumaryczny raport architektoniczny - modul 4 / 10xArchitect

## 1. Opisane projekty

| Repozytorium | Stack | Skala orientacyjna | Artefakty |
|---|---|---|---|
| `HouseholdBudgetMate` | .NET 10, ASP.NET Core Blazor Server, MudBlazor, EF Core, PostgreSQL/Npgsql, Serilog, xUnit, FluentAssertions, NetArchTest, Docker/Render/WiX | Maly, after-hours web app z warstwami `Abstractions`, `Domain`, `Migrations`, `Application`, `Web`, `Tray`, `Installer`, `Tests`; realny hot path skupiony wokol miesiecznego planowania | L2 `context/map/repo-map.md`; L3 `context/archive/2026-06-12-refactor-opportunities/research.md`; L4 `context/archive/2026-06-12-refactor-opportunities/plan.md` i `context/archive/2026-06-14-domain-refactor/plan.md`; L5 `context/domain/*.md` |

W artefaktach L2-L5 nie ma drugiego repozytorium. Wszystkie naglowki/metadane i opisy wskazuja na `HouseholdBudgetMate`.

## 2. Mapa projektu (L2)

- Glowna strefa ryzyka to miesieczny loop budzetowy: `PlanPage`, `ExpenseService`, kontrakty wydatkow i `ExpenseServiceTests`. Mapa repo wskazuje, ze zmiany w tych miejscach dotykaja semantyki planowania miesiaca, nie tylko lokalnych plikow.
- Lokalne centra to `ExpenseService` jako centrum zachowania wydatkow/safe-to-spend, `PlanPage` jako szeroki komponent UI oraz `Program.cs` + `MainLayout.razor` jako laczniki sesji, setupu, nawigacji, admin/readiness i cookies/theme.
- Entry pointy na pierwszy dzien: `context/foundation/prd.md`, `architecture-guide.md`, `PlanPage.razor`, `ExpenseService.cs`, `IExpenseService` i kontrakty wydatkow, `ExpenseServiceTests`, `Program.cs`, `MainLayout.razor`.
- Najwiekszy unknown: dependency-cruiser pokazal prawie pusty graf JS, ale artefakt podkresla, ze realne powiazania ida przez C#/Razor, DI, `IJSRuntime`, globale przegladarki i historie wspolnych zmian.
- Ownership w analizowanym zakresie jest praktycznie jednoosobowy: po odfiltrowaniu botow i agentow mapa wskazuje Kamila Swiderskiego jako kontakt dla hot pathow.

## 3. Analiza ficzera (L3)

Badany przeplyw: post-save orchestration w `PlanPage`, szczegolnie zapis wydatkow i powiazane odswiezanie miesiaca. Wybor wynika bezposrednio ze strefy ryzyka L2: `PlanPage` + `ExpenseService` sa najsilniej sprzezone z miesiecznym planowaniem.

Feature overview: input pochodzi z formularzy Blazor w `PlanPage` dla wydatkow, przychodow, transferow oszczednosci i line items. Stan zmienia sie przez application services (`ExpenseService`, `IncomeService`) oraz zapis EF, z implicit side effects w `ApplicationDbContext.SaveChangesAsync` i audycie. Po zapisie UI zwykle nie ufa zwroconemu DTO, tylko wywoluje `LoadAsync`, ktory odswieza plan miesiaca, summary, incomes, live balance, konta, tag usage, wykresy/KPI i dirty state. Wyjatki od reguly to m.in. target-month copy bez reloadu zrodla oraz suggestion flow z `LoadAsync(bypassPreparation: true)`.

Technical debt:

- Duplicated save behavior: create/edit/delete wydatkow, przychodow, transferow i line items powtarzaja sekwencje service call, cleanup, reload, snackbar. L3 ocenia to jako przypadkowa zlozonosc wokol load-bearing patternu.
- Kruche sprzezenie full reload: `LoadAsync` jest mechanizmem spojnosci po mutacji, wiec proste zastapienie go lokalnym patchowaniem DTO grozi rozjazdem dashboardu, live balance, incomes, tag usage, charts i dirty state.
- Line-item `ActualAmount`: regula jest rozproszona miedzy UI, serwisem, mappingiem, persisted parent state i testami. L3 wskazuje ryzyko, ze Plan/KPI moga czytac effective amount z DTO, a live balance moze zaufac zapisanej kolumnie.
- BRAK artefaktu: w dostarczonym L3/L4 nie ma potwierdzenia ryzyka ast-grepem ani wynikow ast-grep. Nie dopelniam tego domyslem.

## 4. Plan refaktoryzacji (L4)

Wybrana opcja L4 z `2026-06-12-refactor-opportunities`: lokalna, behavior-preserving normalizacja post-save orchestration w `PlanPage`. Docelowy ksztalt: nazwane tryby odswiezania (`full reload`, `bypassPreparation`, `target-copy no current-month reload`, `line-item re-expand`) i prywatny helper/policy w partialach `PlanPage`, przy zachowaniu obecnego `LoadAsync`.

Dodatkowy plan L4 z `2026-06-14-domain-refactor`: granica `MonthlyFinancialPictureDto` jako publiczny read contract miesiecznej rekonsyliacji oraz twardsza polityka effective actual amount, restore i closed-month affordances. Ten plan dotyczy domenowego uporzadkowania po refaktorze UI.

Swiadomie NIE robimy: nie zastepujemy `LoadAsync` lokalnym optimistic patchingiem, nie zmieniamy `IExpenseService`/DTO/domain/migrations w pierwszych fazach UI, nie zmieniamy business rules line-item `ActualAmount`, nie batchujemy `SaveChangesAsync`, nie wprowadzamy MediatR/CQRS, nie rename'ujemy `Expense` do `Post`/`wpis`.

Fazy i weryfikacja:

- L4/UI Phase 1: inventory handlerow + guardrails; weryfikacja `Test-Path`, targeted `dotnet test`, review diff.
- L4/UI Phase 2: expenses-first helper; weryfikacja UI contract tests, `ExpenseServiceTests|AuditTrailTests`, build, `git diff --check`.
- L4/UI Phase 3: incomes, savings transfers, line items; weryfikacja UI/rendered/monthly/service tests i review line-item re-expand.
- L4/UI Phase 4: effective actual amount slice bez migracji; weryfikacja `ExpenseServiceTests`, `MonthlyBudgetingLoopTests`, architecture tests, build.
- L4/UI Phase 5: save-boundary guardrails + browser evidence; weryfikacja audit/user-scope/full suite/build/diff oraz manual smoke.
- L4/domain Phase 1-3: `MonthlyFinancialPicture`, effective actual + restore hardening, UI/evidence; weryfikacja targeted tests, full suite, build, `git diff --check`, manual Plan/Accounts/restore checks.

## 5. Domena wg DDD (L5)

Ubiquitous language:

- `MonthPlan` / plan miesiaca: centralny obiekt MVP flow; w kodzie encja ma publiczne settery, a workflow mieszka w serwisach.
- `Live balance`: poprzednie salda niesavings + due incomes - actual expenses - due savings transfers; w kodzie liczone w `IncomeService`, oddzielnie od planu.
- `Pozostalo w planie`: planned versus actual expenses; nie jest aliasem `Live balance`.
- `Expense` + `Line item`: gdy sa line items, actual amount ma byc suma pozycji; rozjazd polega na persisted `Expense.ActualAmount` i effective amount liczonym w helperze/mappingu.
- `Safe-to-spend`: historyczny/stary kontrakt, jawnie nieobecny jako output MVP; testy pilnuja braku reintrodukcji.

Najwazniejsze rozjazdy model-vs-kod: `Unexpected expense` nie ma pola `IsUnplanned`; `MonthPlan.Status open|closed` jest w kodzie `bool IsClosed`; `EnvelopeBudget` nie istnieje jako osobna encja, jest `Category.EnvelopeLimit`; starsze `No authentication - LAN only` zostalo zastapione PIN-gated access; zamkniety miesiac jest invariantem serwisowym, nie agregatowym.

Niezmiennik #1: jesli `Expense` ma `ExpenseLineItems`, `ActualAmount` musi rownac sie sumie `ExpenseLineItem.Amount`; bez line items moze byc wpisany recznie. Agregat/kandydat straznik: L5 wskazuje `Expense` z `ExpenseLineItems`, a plan refaktoru proponuje docelowy root `TrackedExpense`.

Anti-Corruption Layer: przecieka Chart.js. Zaleznosc przechodzi przez vendored `chart.umd.min.js`, global `window.HBM.charts`, `ChartCanvas`, `ChartDataset`, stringi `"pie"`, `"bar"`, `"line"`, `"mixed"` oraz strony `PlanPage` i `Statistics`. L5 opisuje przeciek przez warstwe Web: od JS adaptera, przez komponent wykresu, az do stron finansowych budujacych dane domenowe.

## 6. Decyzje, ktore naleza do mnie

AI podpowiedzialo ranking ryzyk: najpierw nie ruszac persystencji, tylko nazwac hot pathy, refresh modes i miesieczna granice rekonsyliacji. Samodzielnie rozstrzygam, ze pierwszy bezpieczny krok to nie optymalizacja reloadu, tylko zachowanie `LoadAsync` i usuniecie dryfu handlerow, bo artefakty pokazuja, ze reload jest obecna granica spojnosci. AI wskazalo tez kuszace kierunki DDD, np. `TrackedExpense`; samodzielna decyzja powinna byc taka, zeby nie przepisywac encji EF na bogate agregaty bez testow i bez osobnego planu migracji zachowan. W obszarze ACL wybieram Chart.js jako lepszy kandydat niz Serilog/Npgsql, bo przeciek dotyka stron finansowych i jezyka produktu, a nie tylko infrastruktury.
