---
title: "Plan ACL dla przeciekajacej zaleznosci Chart.js"
created: 2026-06-13
type: refactor-plan
---

# Plan ACL dla przeciekajacej zaleznosci Chart.js

## KROK 0 - Kontekst projektu

Produkt jest aplikacja do planowania budzetu domowego. PRD opisuje problem jako utrate kontroli nad "planned expenses, real spending, multiple accounts, loans, recurring payments, and savings" (`context/foundation/prd.md:20`) i obiecuje, ze aplikacja "reconciles the monthly plan, real expenses, accounts, loans, recurring items, and savings into one live financial picture" (`context/foundation/prd.md:22`). Kryteria sukcesu obejmuja m.in. planowanie miesiaca, zapisywanie wydatkow realnych i nieoczekiwanych, widok Live balance, remaining progress oraz kontekst oszczednosci (`context/foundation/prd.md:30`, `context/foundation/prd.md:31`, `context/foundation/prd.md:32`, `context/foundation/prd.md:33`, `context/foundation/prd.md:34`).

Stack z README: .NET 10, ASP.NET Core Blazor Server, MudBlazor, EF Core, PostgreSQL + Npgsql, Serilog, xUnit, FluentAssertions, NetArchTest, Docker/Compose, Render Blueprint oraz WiX/MSI (`README.md:7`, `README.md:8`, `README.md:9`, `README.md:10`, `README.md:11`, `README.md:12`, `README.md:13`, `README.md:14`, `README.md:15`, `README.md:16`, `README.md:17`, `README.md:18`). Struktura repo jest warstwowa: Abstractions, Domain, Migrations, Application, Web, Tray, Installer, Tests (`README.md:20`, `README.md:21`, `README.md:22`, `README.md:23`, `README.md:24`, `README.md:25`, `README.md:26`, `README.md:27`, `README.md:28`, `README.md:29`, `README.md:30`, `README.md:31`, `README.md:32`, `README.md:33`).

Zadeklarowane granice warstw sa ostre. UI wolno wolac application services, ale nie baze danych bezposrednio (`README.md:38`). Logika biznesowa ma zyc w Application (`README.md:39`), a encje domenowe nie maja wracac do UI; granica publiczna to DTO/request z Abstractions (`README.md:41`). Architecture guide doprecyzowuje, ze Presentation odpowiada za strony, formularze, walidacje UI, mapowanie do request contracts i wolanie application services (`context/foundation/architecture/architecture-guide.md:16`, `context/foundation/architecture/architecture-guide.md:17`, `context/foundation/architecture/architecture-guide.md:18`, `context/foundation/architecture/architecture-guide.md:19`, `context/foundation/architecture/architecture-guide.md:20`, `context/foundation/architecture/architecture-guide.md:21`, `context/foundation/architecture/architecture-guide.md:22`), ale nie za workflow biznesowy, bezposrednie query do bazy ani manipulowanie encjami domenowymi (`context/foundation/architecture/architecture-guide.md:23`, `context/foundation/architecture/architecture-guide.md:24`, `context/foundation/architecture/architecture-guide.md:25`, `context/foundation/architecture/architecture-guide.md:26`, `context/foundation/architecture/architecture-guide.md:27`). Abstractions maja miec zero zewnetrznych zaleznosci poza `System.*` (`context/foundation/architecture/architecture-guide.md:42`, `context/foundation/architecture/architecture-guide.md:52`, `context/foundation/architecture/architecture-guide.md:53`, `context/foundation/architecture/architecture-guide.md:54`).

Istotne zaleznosci z manifestow:

| Projekt | Zaleznosci zewnetrzne |
| --- | --- |
| `HouseholdBudgetMate.Web` | EF Core, EF Design/Tools, MudBlazor, Npgsql EF Core, Serilog.AspNetCore, Serilog.Sinks.PostgreSQL (`src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:29`, `src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:30`, `src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:31`, `src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:32`, `src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:33`, `src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:34`, `src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:35`, `src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:36`, `src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:37`, `src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:38`, `src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:39`, `src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:40`, `src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:41`) |
| `HouseholdBudgetMate.Application` | FluentValidation, PublicHoliday, Serilog, Serilog.AspNetCore, Serilog.Settings.Configuration, Serilog.Sinks.PostgreSQL, TimeZoneConverter, Microsoft.AspNetCore.App (`src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:17`, `src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:18`, `src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:19`, `src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:20`, `src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:21`, `src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:22`, `src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:23`, `src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:27`) |
| `HouseholdBudgetMate.Migrations` | EF Core, EF Relational, Npgsql EF Core (`src/HouseholdBudgetMate.Migrations/HouseholdBudgetMate.Migrations.csproj:11`, `src/HouseholdBudgetMate.Migrations/HouseholdBudgetMate.Migrations.csproj:12`, `src/HouseholdBudgetMate.Migrations/HouseholdBudgetMate.Migrations.csproj:13`) |
| `HouseholdBudgetMate.Domain` | Microsoft.EntityFrameworkCore (`src/HouseholdBudgetMate.Domain/HouseholdBudgetMate.Domain.csproj:11`) |
| `HouseholdBudgetMate.Abstractions` | Brak zewnetrznych pakietow w projekcie (`src/HouseholdBudgetMate.Abstractions/HouseholdBudgetMate.Abstractions.csproj:4`) |

## KROK 1 - Identyfikacja przeciekajacych zaleznosci

| Zaleznosc / obszar | Dzisiejsze miejsca, ktore ja znaja | Sygnał przecieku |
| --- | --- | --- |
| Chart.js + lokalny model `ChartCanvas`/`ChartDataset` | Script Chart.js jest dolaczany w aplikacji (`src/HouseholdBudgetMate.Web/Components/App.razor:59`), vendored plik wskazuje Chart.js 4.4.9 (`src/HouseholdBudgetMate.Web/wwwroot/js/chart.umd.min.js:3`), adapter JS tworzy global `window.HBM.charts` (`src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:1`, `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:2`) i trzyma instancje `Chart` (`src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:5`). `ChartModels.cs` opisuje "Chart.js dataset descriptor" (`src/HouseholdBudgetMate.Web/Components/Charts/ChartModels.cs:3`) oraz parametry `Type` jako `"bar"`, `"line"`, `"pie"` (`src/HouseholdBudgetMate.Web/Components/Charts/ChartModels.cs:8`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartModels.cs:9`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartModels.cs:10`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartModels.cs:11`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartModels.cs:12`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartModels.cs:13`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartModels.cs:14`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartModels.cs:15`). `ChartCanvas` przyjmuje `ChartType` (`src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:13`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:14`) i `ChartDataset[]` (`src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:20`), potem wywoluje `HBM.charts.create` i `HBM.charts.update` (`src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:54`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:55`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:64`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:65`). Strony biznesowe tez znaja typy wykresow: PlanPage trzyma `_pieDatasets` (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Charts.cs:8`), buduje `ChartDataset(..., "pie", ...)` (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Charts.cs:66`) i przekazuje `ChartType="pie"` (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:1157`). Statistics przekazuje `ChartType="mixed"` i `ChartType="line"` (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:545`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:561`) oraz buduje serie `"bar"` i `"line"` (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1323`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1324`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1372`). | Typy i stringi biblioteki renderujacej sa w komponentach user-facing i w logice budujacej dane finansowe do wykresow. Zmiana biblioteki wymaga edycji stron, komponentu, modelu C# i JS. |
| Serilog + Npgsql sink | Application deklaruje pakiety Serilog i sink PostgreSQL (`src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:19`, `src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:20`, `src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:21`, `src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:22`) oraz framework ASP.NET (`src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:27`). `SerilogExtensions` importuje `NpgsqlTypes`, `Serilog`, `Serilog.Events`, `Serilog.Sinks.PostgreSQL` (`src/HouseholdBudgetMate.Application/Kernel/Extensions/SerilogExtensions.cs:8`, `src/HouseholdBudgetMate.Application/Kernel/Extensions/SerilogExtensions.cs:9`, `src/HouseholdBudgetMate.Application/Kernel/Extensions/SerilogExtensions.cs:10`, `src/HouseholdBudgetMate.Application/Kernel/Extensions/SerilogExtensions.cs:11`), rozszerza `WebApplicationBuilder` (`src/HouseholdBudgetMate.Application/Kernel/Extensions/SerilogExtensions.cs:21`) i `WebApplication` (`src/HouseholdBudgetMate.Application/Kernel/Extensions/SerilogExtensions.cs:67`), wywoluje `builder.Host.UseSerilog()` (`src/HouseholdBudgetMate.Application/Kernel/Extensions/SerilogExtensions.cs:57`) oraz mapuje kolumne przez `NpgsqlDbType.Varchar` (`src/HouseholdBudgetMate.Application/Kernel/Extensions/SerilogExtensions.cs:94`). Web zna te rozszerzenia w `Program.cs` (`src/HouseholdBudgetMate.Web/Program.cs:191`, `src/HouseholdBudgetMate.Web/Program.cs:237`). | Application zna hosting ASP.NET i konkretny sink PostgreSQL. To narusza kierunek odpowiedzialnosci, bo Architecture guide mowi, ze Application nie powinna zawierac HTTP protocol concerns (`context/foundation/architecture/architecture-guide.md:38`, `context/foundation/architecture/architecture-guide.md:39`, `context/foundation/architecture/architecture-guide.md:40`). |
| Npgsql / EF Core w setupie migracji | Migrations zna Npgsql provider (`src/HouseholdBudgetMate.Migrations/HouseholdBudgetMate.Migrations.csproj:13`), Web zna Npgsql provider (`src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:39`), a `DatabaseMigrationOrchestrator` importuje Npgsql (`src/HouseholdBudgetMate.Web/Setup/DatabaseMigrationOrchestrator.cs:3`), tworzy `NpgsqlConnection` (`src/HouseholdBudgetMate.Web/Setup/DatabaseMigrationOrchestrator.cs:18`) i konfiguruje `UseNpgsql` (`src/HouseholdBudgetMate.Web/Setup/DatabaseMigrationOrchestrator.cs:40`). | Provider bazy jest w Migrations i Web Setup. To raczej infrastruktura startowa niz domena, ale koszt wymiany PostgreSQL dotknalby startupu i migracji. |
| PublicHoliday | Pakiet jest w Application (`src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:18`) i importowany w `DateTimeExtensions` (`src/HouseholdBudgetMate.Application/Kernel/Extensions/DateTimeExtensions.cs:2`). | Niski przeciek: zaleznosc jest lokalna dla Application, nie przechodzi przez UI/API/kontrakty. |
| TimeZoneConverter | Pakiet jest w Application (`src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj:23`), importowany przez `DateTimeProvider` (`src/HouseholdBudgetMate.Application/Kernel/Timing/DateTimeProvider.cs:3`) i uzywany w `TZConvert.GetTimeZoneInfo` (`src/HouseholdBudgetMate.Application/Kernel/Timing/DateTimeProvider.cs:52`). | Niski przeciek: zaleznosc jest ukryta w providerze czasu, nie w kontraktach domenowych. |
| MudBlazor | MudBlazor jest zaleznoscia Web (`src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj:38`). | UI framework jest szeroki, ale oczekiwany w warstwie Presentation. Brak sygnalu, ze przecieka do Application/Domain/Abstractions. |

## KROK 2 - Klasyfikacja i wybor #1

| Zaleznosc | Dotkniete warstwy / pliki | Koszt wymiany dzis | Intencja z dokumentow | Ocena |
| --- | --- | --- | --- | --- |
| Chart.js + `ChartCanvas`/`ChartDataset` | Web page code, shared chart component, JS interop, global JS vendor, dokumentacja mapujaca zaleznosci (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Charts.cs:66`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:1157`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:545`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:561`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:54`, `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:128`) | Wysoki: wymiana biblioteki lub jej modelu wymaga edycji stron finansowych, komponentu C#, JS interop i skryptu ladowanego w aplikacji. | Dokumentacja change research twierdzi, ze "shared chart component backed by Chart.js through charts.js" istnieje (`context/changes/chart-improvements/research.md:42`) i ze warstwa jest "already well separated" (`context/changes/chart-improvements/research.md:132`). Repo map ostrzega jednak, ze realne powiazanie idzie przez global `window.HBM.charts`, `Chart`, `IJSRuntime` (`context/map/repo-map.md:78`). | Najgorszy przeciek dla planu ACL: bezposrednio dotyka user-facing danych finansowych i ma rozjazd intencja-vs-kod. |
| Serilog + Npgsql sink | Application + Web startup + PostgreSQL sink (`src/HouseholdBudgetMate.Application/Kernel/Extensions/SerilogExtensions.cs:21`, `src/HouseholdBudgetMate.Application/Kernel/Extensions/SerilogExtensions.cs:67`, `src/HouseholdBudgetMate.Web/Program.cs:191`, `src/HouseholdBudgetMate.Web/Program.cs:237`) | Sredni/wysoki: wymiana logowania dotknie Application i Web, mimo ze to infrastruktura. | Architecture guide mowi, ze Application nie powinna miec HTTP concerns (`context/foundation/architecture/architecture-guide.md:38`, `context/foundation/architecture/architecture-guide.md:39`, `context/foundation/architecture/architecture-guide.md:40`). | Silny przeciek architektoniczny, ale operacyjny, nie zwiazany bezposrednio z modelem domenowym uzytkownika. |
| Npgsql / EF setup | Migrations + Web setup (`src/HouseholdBudgetMate.Migrations/HouseholdBudgetMate.Migrations.csproj:13`, `src/HouseholdBudgetMate.Web/Setup/DatabaseMigrationOrchestrator.cs:18`, `src/HouseholdBudgetMate.Web/Setup/DatabaseMigrationOrchestrator.cs:40`) | Sredni: wymiana DB dotyka migracji i startupu. | README jawnie wybiera PostgreSQL + Npgsql (`README.md:13`). | Akceptowalna infrastruktura, nie najlepszy kandydat na ACL domenowy. |
| PublicHoliday | Application only (`src/HouseholdBudgetMate.Application/Kernel/Extensions/DateTimeExtensions.cs:2`) | Niski | Brak deklaracji wymienialnosci. | Nie przecieka przez granice. |
| TimeZoneConverter | Application timing provider only (`src/HouseholdBudgetMate.Application/Kernel/Timing/DateTimeProvider.cs:3`, `src/HouseholdBudgetMate.Application/Kernel/Timing/DateTimeProvider.cs:52`) | Niski | Brak deklaracji wymienialnosci. | Dobrze zamkniete. |

Wybor #1: Chart.js + model `ChartCanvas`/`ChartDataset`.

Uzasadnienie: ten przeciek jest najbardziej kosztowny, bo strony z logika prezentowania finansow konstruuja surowe obiekty zgodne z adapterem Chart.js i podaja stringi typu `"pie"`, `"bar"`, `"line"`, `"mixed"`. Jednoczesnie dokumentacja twierdzi, ze warstwa wykresow jest juz dobrze odseparowana (`context/changes/chart-improvements/research.md:132`, `context/changes/chart-improvements/research.md:133`), a mapa repo wskazuje ukryte globalne powiazania (`context/map/artifact-2-structure.md:66`, `context/map/repo-map.md:78`). To daje mocny sygnal rozjazdu intencja-vs-kod.

## KROK 3 - Diagnoza wybranego przecieku

### Duplikacja typu wykresu

Ten sam slownik renderera powtarza sie w C#, Razor i JavaScript:

- `ChartDataset.Type` dokumentuje typy `"bar"`, `"line"`, `"pie"` (`src/HouseholdBudgetMate.Web/Components/Charts/ChartModels.cs:8`) i ma domyslne `"bar"` (`src/HouseholdBudgetMate.Web/Components/Charts/ChartModels.cs:15`).
- `ChartCanvas.ChartType` dokumentuje `"bar"`, `"line"`, `"pie"`, `"mixed"` (`src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:13`) i ma domyslne `"bar"` (`src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:14`).
- `charts.js` rozpoznaje `pie`, `doughnut`, `line`, `bar`, `mixed` (`src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:29`, `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:30`, `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:51`, `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:52`, `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:64`, `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:65`, `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:77`, `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:128`, `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:130`).
- PlanPage uzywa `"pie"` w C# i Razor (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Charts.cs:66`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:1157`).
- Statistics uzywa `"mixed"`, `"line"`, `"bar"` w Razor/C# (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:545`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:561`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1323`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1324`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1372`).

### Granica, ktora przecieka

`ChartCanvas` wyglada jak komponent izolujacy, ale jego publiczny kontrakt jest juz kontraktem biblioteki renderujacej: `ChartType` jako string i `ChartDataset[]` (`src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:13`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:20`). `BuildJsDatasets` przepisuje wprost pola `label`, `data`, `backgroundColor`, `borderColor`, `type`, `backgroundColors` (`src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:71`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:72`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:73`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:74`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:75`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:76`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:77`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:78`). To znaczy, ze strona finansowa nie mowi "pokaz trend wydatkow" albo "pokaz udzial kategorii"; ona mowi adapterowi, jakiego typu dataset ma narysowac.

W JavaScript przeciek jest mocniejszy, bo adapter zaklada globalny konstruktor `Chart`. Repo map nazwal to ukryta zaleznoscia: `charts.js` i `ChartCanvas.razor` lacza sie przez global `window.HBM.charts` oraz global constructor `Chart` (`context/map/artifact-2-structure.md:66`), a szersza mapa pokazuje, ze coupling idzie przez `window.HBM.charts`, `Chart`, `IJSRuntime` i nie jest widoczny w statycznym grafie (`context/map/repo-map.md:78`).

### Rozjazd intencja-vs-kod

Research dla wykresow stwierdza, ze obecny "shared chart component backed by Chart.js through charts.js" wspiera bar/line/pie/mixed (`context/changes/chart-improvements/research.md:42`) oraz ze "chart layer is already well separated" (`context/changes/chart-improvements/research.md:132`). Kod temu nie dotrzymuje: strony biznesowe musza znac typy `ChartDataset`, `ChartType` i stringi Chart.js (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Charts.cs:66`, `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:1157`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:545`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:561`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1323`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1324`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1372`).

To nie jest przeciek biblioteki serwerowej do bundla klienta. To przeciek kontraktu biblioteki klienckiej w gore, do stron, ktore powinny operowac na semantyce finansowej.

## KROK 4 - Projekt ACL

### Cel

Wprowadzic waski antykorupcyjny model wykresow finansowych. Strony maja tworzyc semantyczne specyfikacje wykresow, a nie surowe dataset-y Chart.js. Jedynym miejscem wiedzy o Chart.js ma byc adapter renderowania.

### Value object / model wejsciowy ACL

Proponowane nazwy load-bearing:

- `FinancialChartSpec` - semantyczny opis wykresu finansowego.
- `FinancialChartKind` - intencja wykresu, np. `CategoryShare`, `MonthlyPlanVsActual`, `CategoryTrend`.
- `FinancialChartSeries` - seria danych z rola, nie z typem biblioteki.
- `FinancialChartSeriesRole` - `ActualSpending`, `PlannedSpending`, `CategoryAmount`, `Trend`.
- `FinancialChartPoint` - etykieta + wartosc.
- `FinancialChartPalette` - semantyka kolorow, bez `backgroundColor`/`borderColor` w stronach.
- `IChartRendererPort` - waski port renderowania dla komponentu.
- `ChartJsRendererAdapter` - jedyne miejsce mapowania do Chart.js.
- `UnsupportedFinancialChartSpecException` - blad dla nieobslugiwanej kombinacji semantycznej.

Pseudokod:

```csharp
public sealed record FinancialChartSpec(
    FinancialChartKind Kind,
    IReadOnlyList<string> Labels,
    IReadOnlyList<FinancialChartSeries> Series,
    FinancialChartPalette Palette)
{
    public static FinancialChartSpec CategoryShare(
        IReadOnlyList<string> categoryNames,
        IReadOnlyList<decimal> amounts,
        FinancialChartPalette palette);

    public static FinancialChartSpec MonthlyPlanVsActual(
        IReadOnlyList<string> monthLabels,
        IReadOnlyList<decimal> actual,
        IReadOnlyList<decimal> planned,
        FinancialChartPalette palette);

    public static FinancialChartSpec CategoryTrend(
        IReadOnlyList<string> periodLabels,
        IReadOnlyList<decimal> values,
        FinancialChartPalette palette);
}

public sealed record FinancialChartSeries(
    string Label,
    FinancialChartSeriesRole Role,
    IReadOnlyList<decimal> Values);

public enum FinancialChartKind
{
    CategoryShare,
    MonthlyPlanVsActual,
    CategoryTrend
}

public enum FinancialChartSeriesRole
{
    CategoryAmount,
    ActualSpending,
    PlannedSpending,
    Trend
}
```

Preconditions:

```csharp
FinancialChartSpec.MonthlyPlanVsActual(labels, actual, planned, palette):
    require labels.Count == actual.Count
    require labels.Count == planned.Count
    require all values >= 0
    return spec with semantic roles ActualSpending + PlannedSpending

FinancialChartSpec.CategoryShare(categoryNames, amounts, palette):
    require categoryNames.Count == amounts.Count
    require all amounts >= 0
    require at least one amount > 0
    return spec with Kind CategoryShare
```

Nielegalny stan rzuca nazwany blad domenowo-prezentacyjny, np. `InvalidFinancialChartSpecException`. Nie ma cichego fallbacku do `"bar"`.

### Waski port i adapter

Port:

```csharp
public interface IChartRendererPort
{
    ValueTask RenderAsync(
        ElementReference canvas,
        string chartId,
        FinancialChartSpec spec,
        ChartRenderTheme theme,
        CancellationToken cancellationToken);

    ValueTask UpdateAsync(
        string chartId,
        FinancialChartSpec spec,
        ChartRenderTheme theme,
        CancellationToken cancellationToken);

    ValueTask DestroyAsync(
        string chartId,
        CancellationToken cancellationToken);
}
```

Adapter Chart.js:

```csharp
internal sealed class ChartJsRendererAdapter : IChartRendererPort
{
    public ValueTask RenderAsync(..., FinancialChartSpec spec, ...)
    {
        var payload = ChartJsPayloadMapper.Map(spec);
        return js.InvokeVoidAsync(
            "HBM.charts.create",
            canvas,
            chartId,
            payload.ChartType,
            payload.Labels,
            payload.Datasets,
            theme.IsDark);
    }
}

internal static class ChartJsPayloadMapper
{
    public static ChartJsPayload Map(FinancialChartSpec spec) =>
        spec.Kind switch
        {
            FinancialChartKind.CategoryShare => MapPie(spec),
            FinancialChartKind.MonthlyPlanVsActual => MapMixed(spec),
            FinancialChartKind.CategoryTrend => MapLine(spec),
            _ => throw new UnsupportedFinancialChartSpecException(spec.Kind)
        };
}
```

Miejsce decyzji o `"pie"`, `"mixed"`, `"bar"`, `"line"`, `backgroundColor`, `borderColor`, `type` przenosi sie do `ChartJsPayloadMapper`. `PlanPage` i `Statistics` tworza tylko `FinancialChartSpec`.

### Docelowy komponent

`FinancialChartCanvas` powinien przyjmowac jeden parametr domenowo-prezentacyjny:

```csharp
[Parameter, EditorRequired]
public FinancialChartSpec Chart { get; set; } = default!;
```

Komponent nie ujawnia `ChartType`, `ChartDataset`, ani stringow biblioteki. Jesli Chart.js zostanie zastapiony inna biblioteka, zmienia sie implementacja `IChartRendererPort` i mapper, a nie strony finansowe.

## KROK 5 - Dowod izolacji i before/after

### Before / after

| Dzisiejsze miejsce | Before | After |
| --- | --- | --- |
| `PlanPage.Charts.cs` | Buduje `ChartDataset(..., "pie", sliceColors)` (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Charts.cs:66`). | Buduje `FinancialChartSpec.CategoryShare(categoryNames, amounts, palette)`. Nie zna `"pie"` ani `ChartDataset`. |
| `PlanPage.razor` | Przekazuje `ChartType="pie"` do `ChartCanvas` (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:1157`). | Przekazuje `Chart="@_categoryShareChart"` do `FinancialChartCanvas`. |
| `Statistics.razor` | Przekazuje `ChartType="mixed"` i `ChartType="line"` (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:545`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:561`). | Przekazuje semantyczne specyfikacje `MonthlyPlanVsActual` i `CategoryTrend`. |
| `Statistics.razor` | Buduje serie `"bar"` i `"line"` (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1323`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1324`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1372`). | Buduje `FinancialChartSeries` z rolami `ActualSpending`, `PlannedSpending`, `Trend`. |
| `ChartCanvas.razor` | Publiczne parametry to `ChartType` i `ChartDataset[]` (`src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:13`, `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor:20`). | Publiczny parametr to `FinancialChartSpec`; port renderera mapuje spec na payload biblioteki. |
| `charts.js` | Jest jedynym wykonawca `new Chart(...)`, ale nie jedynym wlascicielem slownika typow (`src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:130`, `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js:136`). | Dalej moze byc adapterem JS dla Chart.js, ale jego kontrakt jest konsumowany tylko przez `ChartJsRendererAdapter`. |

### Dowod izolacji po refaktorze

Wymiana Chart.js po refaktorze powinna dotknac tylko:

- `src/HouseholdBudgetMate.Web/Components/Charts/AntiCorruption/ChartJsRendererAdapter.cs`
- `src/HouseholdBudgetMate.Web/Components/Charts/AntiCorruption/ChartJsPayloadMapper.cs`
- `src/HouseholdBudgetMate.Web/wwwroot/js/charts.js`
- `src/HouseholdBudgetMate.Web/Components/App.razor` tylko jesli zmienia sie sposob ladowania skryptu (`src/HouseholdBudgetMate.Web/Components/App.razor:59`)
- testy adaptera/ACL

Pliki, ktore dzis znaja zaleznosc, a po refaktorze nie powinny:

- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Charts.cs`
- `src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor`
- `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor`
- `src/HouseholdBudgetMate.Web/Components/Charts/ChartModels.cs` albo jego nastepca publiczny
- `src/HouseholdBudgetMate.Web/Components/Charts/ChartCanvas.razor` w publicznym API komponentu

Otwarte pytania z researchu dotycza raczej wyborow produktowych niz kontraktu Chart.js: czy najpierw poprawiac dashboard czy Statistics, czy pokazac account balance chart, czy category trends, ile kategorii laczyc do "Other" (`context/changes/chart-improvements/research.md:143`, `context/changes/chart-improvements/research.md:144`, `context/changes/chart-improvements/research.md:145`). Decyzje te powinny byc zakodowane jako `FinancialChartKind` i fabryki `FinancialChartSpec`, nie jako stringi konkretnej biblioteki w stronach.

## KROK 6 - Weryfikacja i plan faz

### Kryterium sukcesu

Po refaktorze grep po nazwach i kontraktach Chart.js:

```powershell
rg -n "ChartDataset|ChartType|`"bar`"|`"line`"|`"pie`"|`"mixed`"|HBM\.charts|new Chart|chart\.umd" src/HouseholdBudgetMate.Web
```

powinien zwracac wylacznie pliki ACL/adaptera, `charts.js`, `App.razor` dla ladowania vendora oraz testy adaptera. Nie powinien zwracac `PlanPage.Charts.cs`, `PlanPage.razor`, `Statistics.razor` ani publicznego API komponentu wykresu.

### Plan faz

1. Test-first: dodac test architektoniczny/statyczny, ktory wykrywa `ChartDataset`, `ChartType`, `"bar"`, `"line"`, `"pie"`, `"mixed"` poza katalogiem ACL/adaptera i poza `charts.js`. Ten test najpierw powinien byc czerwony.
2. Wprowadzic `FinancialChartSpec`, `FinancialChartSeries`, role serii i bledy walidacyjne w obszarze `Web/Components/Charts`, bez zmiany zachowania UI.
3. Wprowadzic `IChartRendererPort`, `ChartJsRendererAdapter` i mapper payloadu. W tej fazie tylko adapter zna `HBM.charts` i typy Chart.js.
4. Zmienic `ChartCanvas`/nowy `FinancialChartCanvas`, aby publiczny kontrakt przyjmowal `FinancialChartSpec`, a nie `ChartType`/`ChartDataset[]`.
5. Migrowac PlanPage: `_pieDatasets` (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.Charts.cs:8`) zamienic na semantyczny `FinancialChartSpec.CategoryShare`.
6. Migrowac Statistics: `_barDatasets` i `_lineDatasets` (`src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1233`, `src/HouseholdBudgetMate.Web/Components/Pages/Statistics.razor:1234`) zamienic na `MonthlyPlanVsActual` i `CategoryTrend`.
7. Usunac lub ukryc stary publiczny `ChartDataset`/`ChartType`. Test statyczny powinien byc zielony.
8. Uruchomic istniejace testy UI/contract dotyczace miesiecznego loopa i statystyk oraz pelny build. Konwencja projektu wskazuje `dotnet test HouseholdBudgetMate.slnx` jako standard (`README.md:61`).

### Minimalny zestaw testow ACL

- `CategoryShare` odrzuca puste dane i serie, gdzie wszystkie kwoty sa zerowe.
- `MonthlyPlanVsActual` odrzuca serie o roznych dlugosciach.
- `CategoryTrend` nie pozwala na wartosci ujemne, jesli wykres reprezentuje wydatki.
- Mapper Chart.js mapuje `CategoryShare` na typ lokalnego adaptera tylko w `ChartJsPayloadMapper`, nie w stronie.
- Test architektoniczny potwierdza, ze `PlanPage` i `Statistics` nie zawieraja `ChartDataset`, `ChartType` ani stringow typow biblioteki.

## Rekomendacja

Pierwszym refaktorem powinien byc ACL dla Chart.js, nie dlatego, ze Chart.js jest najwieksza zaleznoscia w repo, ale dlatego, ze jego model wszedl do miejsc, ktore powinny mowic jezykiem finansowym produktu. Serilog/Npgsql w Application jest osobnym, waznym sprzataniem architektonicznym, lecz mniej bezposrednio zmienia model uzytkownika. ACL dla wykresow ma jasne kryterium sukcesu: strony finansowe przestaja znac renderer, a renderer staje sie wymienialnym adapterem.
