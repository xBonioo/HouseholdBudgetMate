# Artifact 2: Structure Map

Data: analiza wykonana 2026-06-09 jako kontynuacja `context/map/artifact-1-territory.md`.

Cel: uzyc `dependency-cruiser` do sprawdzenia struktury zaleznosci w aktywnych obszarach webapp oraz wskazac ryzyka testowalnosci wynikajace z importow, globalnego stanu, JS interop i platformowych typow przegladarki.

## Zakres

Analizowany zakres zrodel:

```powershell
npx depcruise --config .dependency-cruiser.cjs --output-type json --output-to artifacts/dependency-cruiser-webapp-testability.json src/HouseholdBudgetMate.Web/Components src/HouseholdBudgetMate.Web/wwwroot/js
npx depcruise --config .dependency-cruiser.cjs --output-type metrics src/HouseholdBudgetMate.Web/Components src/HouseholdBudgetMate.Web/wwwroot/js
```

Nie generowano Graphviz/DOT.

Zakres zostal celowo ograniczony do zrodel webapp. Szeroki skan `src/HouseholdBudgetMate.Web` lapal artefakty z `bin/Release/.../publish/wwwroot/_framework/blazor.web.js`, co jest build outputem i bylo filtrowane jako szum zgodnie z podejsciem z `artifact-1-territory.md`.

## Wynik Dependency Cruiser

`dependency-cruiser` w aktywnych zrodlach webapp znalazl:

- `4 modules`
- `0 dependencies cruised`
- `0 dependency violations`
- `0` import cycles

Moduly widziane przez cruisera:

| Modul | Importy | Status |
|---|---:|---|
| `src/HouseholdBudgetMate.Web/Components/Layout/ReconnectModal.razor.js` | 0 | valid, orphan |
| `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.js` | 0 | valid, orphan |
| `src/HouseholdBudgetMate.Web/wwwroot/js/backup-drop-zone.js` | 0 | valid, orphan |
| `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js` | 0 | valid, orphan |

Metryki rowniez potwierdzily brak klasycznego grafu importow:

| Modul / folder | N | Ca | Ce | I |
|---|---:|---:|---:|---:|
| `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor.js` | 1 | 0 | 0 | 0% |
| `src/HouseholdBudgetMate.Web/Components/Layout/ReconnectModal.razor.js` | 1 | 0 | 0 | 0% |
| `src/HouseholdBudgetMate.Web/wwwroot/js/backup-drop-zone.js` | 1 | 0 | 0 | 0% |
| `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js` | 1 | 0 | 0 | 0% |

Interpretacja: w warstwie JS/TS nie ma cykli ani dlugich lancuchow importow. To nie znaczy, ze nie ma sprzezen. Sprzezenia sa ukryte w globalach, DOM, JS interop, komponentach Razor i serwisach C#.

## Najwazniejsze Obserwacje

- Najaktywniejsze obszary z `artifact-1-territory.md` (`PlanPage`, `MainLayout`, `Program.cs`, `Accounts`, `Home`, `Statistics`, backup/admin) nie maja istotnego grafu importow JS/TS.
- `dependency-cruiser` jest dobrym narzedziem do wykrywania cykli i zaleznosci w JS/TS, ale w tym repo nie widzi wiekszosci realnych zaleznosci C#/Razor.
- Brak importow w JS oznacza, ze obecne skrypty sa testowalne tylko pozornie: wiele zalezy od `window`, `document`, `Blazor`, `Chart`, `DataTransfer`, eventow przegladarki i `IJSRuntime`.
- Dla aktywnych obszarow testowalnosc czesciej bedzie problemem szerokich komponentow Blazor i wielu wstrzyknietych serwisow niz problemem cykli JS.
- Najbardziej wrazliwe terytorium pozostaje miesieczny loop budzetowy: `PlanPage` + `ExpenseService` + kontrakty wydatkow + testy `ExpenseServiceTests`.

## Ryzyka Testowalnosci

| Obszar | Ryzyko | Preferowany typ testu |
|---|---|---|
| `Web/Components/Pages/PlanPage` | Wiele serwisow, JS interop, nawigacja, snackbary, dialogi i stan miesiecznego loopa w jednym user-facing obszarze. Izolowany test komponentu wymaga wielu mockow. | Logika finansowa w testach uslugowych/integracyjnych; glowne przeplywy UI w render/e2e. |
| `Web/Components/Pages/PlanPage/PlanPage.razor.js` | Globalne funkcje na `window`, pomiary DOM, `resize`, `DotNetObjectReference`. | Test JS z DOM harness albo e2e dla zachowan viewport/scroll. |
| `Web/Components/Layout/MainLayout.razor` | Shell aplikacji laczy sesje, nawigacje, cache miesiecy, dialog logowania, cookie/theme i JS runtime. | Testy komponentowe tylko dla malych regulek; sesja/theme/navigation jako integracyjne lub e2e. |
| `Web/Components/Layout/ReconnectModal.razor.js` | Zaleznosc od globalnego `Blazor`, `document.visibilityState` i eventow dokumentu. | E2E/scenariusz reconnect lub test JS z mockowanym runtime Blazor. |
| `Web/Components/Pages/AdminBackup.razor` + `wwwroot/js/backup-drop-zone.js` | Drag/drop, `DataTransfer`, `input.files`, download przez JS i serwis backupu. | Serwis backupu testowac integracyjnie; drag/drop i download testem e2e. |
| `wwwroot/js/charts.js` + `Components/Charts/ChartCanvas.razor` | Global `window.HBM.charts` i globalny konstruktor `Chart`; cruiser nie widzi tej zaleznosci, bo nie jest importem. | Testowac kontrakt danych do wykresu; wizualne zachowanie przez e2e/screenshot. |
| `Web/Components/Pages/Accounts`, `Home`, `Statistics` | Strony agreguja kilka serwisow aplikacyjnych, nawigacje, snackbar i date provider. | Obliczenia w testach uslugowych; krytyczne KPI/nawigacje w testach renderowanych lub e2e. |

## Najbardziej Podejrzane Moduly

1. `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor`
   - Najaktywniejszy obszar mapy terytorium.
   - Wstrzykuje `IExpenseService`, `ICategoryService`, `IIncomeService`, `IAccountService`, `IJSRuntime`, `ISnackbar`, `NavigationManager`, `ArchiveMonthsCacheService`, `IUserSessionService`, `IDialogService`.
   - Naturalnie wymaga mieszanki testow: service/integration/render/e2e.

2. `src/HouseholdBudgetMate.Web/Components/Layout/MainLayout.razor`
   - Plik-lacznik z `artifact-1-territory.md`.
   - Zalezy od sesji, nawigacji, cache, dialogow i cookies/theme przez JS.
   - Ryzyko: mala zmiana w shellu moze wymagac wielu mockow lub testu integracyjnego.

3. `src/HouseholdBudgetMate.Web/Components/Pages/AdminBackup.razor`
   - Aktywny epizod backup/restore z tygodnia 2026-06-08.
   - Laczy serwis backupu, sesje, JS module import, file input, download i dialogi.
   - Ryzyko: czysto jednostkowy test bedzie sztuczny, bo kluczowy flow jest przegladarkowy.

4. `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js`
   - Brak importow, ale zaleznosc od globalnego `Chart`.
   - Ryzyko: zaleznosc nie pojawi sie w grafie dependency-cruiser, wiec moze byc latwo przeoczona.

5. `src/HouseholdBudgetMate.Web/Components/Pages/Accounts.razor.cs`
   - Aktywny plik z mapy terytorium.
   - Wstrzykuje wiele serwisow domenowych (`IAccountService`, `ICategoryService`, `IExpenseService`, `IIncomeService`, `ILoanService`) oraz `IDialogService`, `ISnackbar`.
   - Ryzyko: test komponentowy moze stac sie testem konfiguracji mockow, nie zachowania uzytkownika.

## Wnioski Strukturalne

Obecny webapp nie ma problemu klasycznych cykli importow JS/TS. Ma za to typowy dla Blazor legacy problem testowalnosci: szerokie komponenty UI sa punktami agregacji serwisow, stanu przegladarki, nawigacji i JS interop.

W praktyce oznacza to:

- Nie warto szukac dlugu tylko przez cykle JS, bo graf importow jest prawie pusty.
- Warto rozdzielac testy wedlug natury ryzyka:
  - logika finansowa i kontrakty: testy uslugowe/integracyjne,
  - render i proste stany komponentu: testy renderowane,
  - DOM/resize/drag/drop/download/reconnect/theme cookie: e2e lub testy browser-like,
  - zaleznosci globalne JS: male adaptery albo jawne moduly ES, jesli obszar bedzie dalej rosl.
- Jezeli celem jest dalsze uzycie `dependency-cruiser`, najbardziej wartosciowy kolejny krok to migracja globalnych skryptow na jawne moduly/importy albo osobny generator grafu zaleznosci C#/Razor.

## Opcjonalny Kolejny Krok: Graf

Na tym etapie graf Graphviz/DOT nie byl generowany. Gdyby byl potrzebny kolejny artefakt wizualny, sensowniejsze opcje to:

- lekki Mermaid dla `Components` + `wwwroot/js`, z oczekiwaniem, ze pokaze glownie samotne moduly;
- osobny graf zaleznosci C#/Razor dla aktywnych obszarow z `artifact-1-territory.md`, bo tam znajduje sie realny ciezar strukturalny repo.
