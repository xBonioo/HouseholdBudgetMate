# Household Budget Mate

Household Budget Mate to webowa aplikacja do prowadzenia budżetu domowego. Aktualny zakres projektu to aplikacja web dla jednego gospodarstwa domowego: planowanie miesiąca, zapisywanie rzeczywistych i nieoczekiwanych wydatków, przychody, konta, kredyty, oszczędności, profile PIN oraz audyt zmian.

Najważniejszym widokiem jest bieżący obraz miesiąca: `Live balance`, `Pozostało w planie`, kontekst oszczędności i informacja, kiedy wynik wymaga uzupełnienia brakujących sald kont. Aplikacja nie ma obecnie publicznego Web API. API pod integracje, np. Home Assistant albo inne scenariusze smart home, jest kierunkiem na przyszłość.

## Stack

- .NET 10
- ASP.NET Core Blazor Server
- MudBlazor
- Entity Framework Core
- PostgreSQL + Npgsql
- Serilog
- xUnit, FluentAssertions, NetArchTest
- Docker / Docker Compose
- Render Blueprint
- WiX/MSI dla instalatora Windows

## Struktura rozwiązania

```text
HouseholdBudgetMate.slnx
src/
  HouseholdBudgetMate.Abstractions/  # publiczne kontrakty, DTO, requesty, interfejsy, enumy
  HouseholdBudgetMate.Domain/        # encje, konfiguracje EF, bazowe typy domenowe
  HouseholdBudgetMate.Migrations/    # ApplicationDbContext i migracje EF Core
  HouseholdBudgetMate.Application/   # serwisy aplikacyjne, walidacja, mapowanie, logika użycia
  HouseholdBudgetMate.Web/           # Blazor Server, strony, komponenty, middleware, setup
  HouseholdBudgetMate.Tray/          # pomocnicza aplikacja tray dla lokalnego użycia
  HouseholdBudgetMate.Installer/     # projekt instalatora MSI
  HouseholdBudgetMate.Tests/         # testy usług, setupu, UI contract i architektury
context/                             # wymagania, architektura, test plan, wdroĹĽenie, zmiany i archiwum
```

## Zasady architektury

- UI w `HouseholdBudgetMate.Web` woła serwisy aplikacyjne, a nie bazę danych bezpośrednio.
- Logika przypadków użycia mieszka w `HouseholdBudgetMate.Application`.
- Serwisy aplikacyjne używają `IDbContextFactory<ApplicationDbContext>` i tworzą kontekst per operacja.
- Encje domenowe nie są zwracane do UI. Granicą zewnętrzną są DTO i requesty z `HouseholdBudgetMate.Abstractions`.
- `ApplicationDbContext` i migracje EF Core są w `HouseholdBudgetMate.Migrations`.
- Mapowanie jest jawne, najczęściej w rozszerzeniach w `HouseholdBudgetMate.Application/Mapping`.
- Walidacja requestów odbywa się w serwisach aplikacyjnych przez FluentValidation.
- Czas w logice aplikacyjnej powinien przechodzić przez `IDateTimeProvider`.

## Wymagania lokalne

- .NET SDK 10
- PostgreSQL
- Docker Desktop, jeżeli uruchamiasz wariant kontenerowy
- PowerShell 5+ na Windows
- WiX Toolset, jeżeli budujesz instalator MSI

Domyślna konfiguracja developerska używa połączenia z `src/HouseholdBudgetMate.Web/appsettings.Development.json`:

```text
Host=localhost;Port=5432;Database=household_budget_mate_dev;Username=postgres;Password=postgres
```

## Szybkie uruchomienie lokalne

Uruchom aplikację web:

```powershell
dotnet run --project src/HouseholdBudgetMate.Web
```

Uruchom testy:

```powershell
dotnet test
```

Zbuduj rozwiązanie:

```powershell
dotnet build HouseholdBudgetMate.slnx
```

## Docker Compose

Lokalny Docker Compose uruchamia PostgreSQL i aplikację web:

```powershell
docker compose up --build
```

Aplikacja będzie dostępna pod:

```text
http://localhost:10000
```

Przy pierwszym wejściu lokalny wariant kontenerowy może przejść przez `/setup`. Dla bazy z `docker-compose.yml` wpisz:

```text
Host: postgres
Port: 5432
Login: household_budget_mate
Hasło: household_budget_mate
Nazwa bazy: household_budget_mate
```

## Konfiguracja runtime

Aplikacja może brać connection string z konfiguracji .NET albo z runtime setupu.

Najważniejsze lokalizacje i zmienne:

- `%APPDATA%\HouseholdBudgetMate\config.json` - runtime config zapisany przez `/setup`.
- `%APPDATA%\HouseholdBudgetMate\files` - katalog plików aplikacji.
- `HOUSEHOLDBUDGETMATE_DATA_DIR` - nadpisuje katalog danych, używane głównie w kontenerach.
- `ConnectionStrings:DefaultConnection` - standardowy connection string .NET.
- `DATABASE_URL`, `POSTGRES_URL`, `POSTGRESQL_URL`, `DATABASE_CONNECTION_STRING` - obsługiwane zmienne środowiskowe dla PostgreSQL.
- `/setup` - panel konfiguracji, gdy aplikacja nie ma kompletnej konfiguracji bazy.
- `/health/ready` - readiness check połączenia z bazą.

W trybie developerskim aplikacja używa `appsettings.Development.json`. W trybie instalowanym lub kontenerowym bez connection stringa aplikacja przekierowuje do `/setup`, zapisuje konfigurację w katalogu danych i używa jej przy kolejnych startach.

## Migracje EF Core

Migracje są trzymane w projekcie `HouseholdBudgetMate.Migrations`, a startup project dla narzędzi EF Core to `HouseholdBudgetMate.Web`.

Dodanie migracji:

```powershell
dotnet ef migrations add AddLoanPercentageChargeAndOriginalInfo --project src/HouseholdBudgetMate.Migrations --startup-project src/HouseholdBudgetMate.Web
```

Usunięcie ostatniej, jeszcze nieutrwalonej migracji:

```powershell
dotnet ef migrations remove --project src/HouseholdBudgetMate.Migrations --startup-project src/HouseholdBudgetMate.Web
```

`dotnet ef migrations remove` usuwa ostatnią migrację z kodu. To jest bezpieczne głównie wtedy, gdy migracja nie została jeszcze zastosowana do bazy z ważnymi danymi. Jeżeli migracja poszła już na realną bazę, nie traktuj usunięcia plików jako rollbacku danych. Wtedy przygotuj kolejną migrację naprawczą albo świadomy plan rollbacku bazy z backupu.

Aplikacja ma domyślnie `Application:MigrateDatabaseOnStart=true`, więc przy starcie uruchamia oczekujące migracje, gdy ma poprawną konfigurację bazy. Lokalny `/setup` również waliduje połączenie i wykonuje migracje przed zapisaniem konfiguracji.

Przed migracją na realnych danych zrób co najmniej:

1. Backup bazy.
2. Krótki przegląd wygenerowanej migracji.
3. Restore smoke test, jeżeli zmiana dotyka ważnych danych.
4. Sprawdzenie `/health/ready` po starcie aplikacji.

## Budowanie instalatora MSI

Zbuduj projekt instalatora:

```powershell
dotnet build "F:\Kamil\.Net\_projects\HouseholdBudgetMate\src\HouseholdBudgetMate.Installer\HouseholdBudgetMate.Installer.wixproj" -c Release
```

Albo użyj skryptu:

```powershell
powershell -ExecutionPolicy Bypass -File "F:\Kamil\.Net\_projects\HouseholdBudgetMate\files\build-msi.ps1" -Configuration Release
```

Wariant instalowany uruchamia web app lokalnie po HTTP/HTTPS zgodnie z `WebHosting` w `appsettings.json`. Dla lokalnego HTTPS aplikacja tworzy certyfikat self-signed dla `localhost` i próbuje dodać go do zaufanych certyfikatów bieżącego użytkownika. Przy pierwszym uruchomieniu przeglądarka może pokazać ostrzeżenie SSL.

## Deployment

### Windows / MSI

1. Zbuduj instalator MSI.
2. Zainstaluj aplikację.
3. Uruchom ją ze skrótu w Start Menu.
4. Przy pierwszym starcie uzupełnij `/setup`, jeżeli aplikacja nie ma konfiguracji bazy.
5. Po poprawnym starcie aplikacja uruchomi oczekujące migracje.

### Docker

Dockerfile publikuje `HouseholdBudgetMate.Web` jako aplikację ASP.NET Core w kontenerze. W kontenerze aplikacja używa HTTP i portu z `PORT`, jeżeli jest ustawiony.

### Render

`render.yaml` definiuje:

- usługę web Docker `household-budget-mate-web`,
- bazę PostgreSQL `household-budget-mate-db`,
- zmienną `DATABASE_URL` pobieraną z bazy Render,
- health check na `/health/ready`.

Render kończy HTTPS na swoim load balancerze, a kontener słucha po HTTP na `0.0.0.0:$PORT`. Aplikacja obsługuje nagłówki `X-Forwarded-Proto` w trybie kontenerowym/chmurowym.

Ważne zmienne dla Render:

```text
ASPNETCORE_ENVIRONMENT=Production
HOUSEHOLDBUDGETMATE_CONTAINER=true
HOUSEHOLDBUDGETMATE_DATA_DIR=/var/lib/householdbudgetmate
Application__MigrateDatabaseOnStart=true
Application__SeedDataToDatabase=false
WebHosting__EnableLanAccess=false
Blazor__DetailedErrors=false
FileStorage__EnablePublicFileServing=false
DATABASE_URL=<z bazy Render>
```

Dane biznesowe są w PostgreSQL. Katalog `HOUSEHOLDBUDGETMATE_DATA_DIR` trzyma lokalny config, pliki i certyfikaty. Bez persistent disk na Render ten katalog jest efemeryczny, więc trwałe uploady wymagają osobnego persistent disk.

## Bezpieczeństwo i realne dane

- Dane budżetu nie powinny być widoczne przed odblokowaniem profilu PIN.
- Profile mają role admin/member.
- `default-user` jest technicznym właścicielem wspólnego budżetu i nie powinien służyć jako interaktywny profil logowania.
- `AuditLogs` są historią zmian finansowych i nie są czyszczone przez retencję logów operacyjnych.
- `Logs` są diagnostyczne i podlegają retencji `Application:LogRetentionDays`.
- `FileStorage__EnablePublicFileServing` domyślnie zostaje wyłączone.
- `Blazor__DetailedErrors` w produkcji powinno pozostać `false`.

Przed wpisaniem realnych danych gospodarstwa upewnij się, że masz backup, sprawdzony restore, działający `/health/ready` i jasny plan dla migracji.

## Testy i jakość

Podstawowe komendy:

```powershell
dotnet test
```

```powershell
dotnet build HouseholdBudgetMate.slnx
```

Testy obejmują m.in.:

- reguły usług aplikacyjnych,
- konfigurację i setup,
- dostęp PIN i granice sesji,
- miesięczną pętlę budżetową,
- audyt,
- architekturę i kierunek zależności.

Nowe testy powinny chronić zachowanie użytkownika albo istotną granicę techniczną. Nie warto pisać testów, które tylko kopiują implementację.

## Roadmapa

Aktualnie projekt jest aplikacją web. Najbliższy kierunek rozwoju to dalsze usprawnienia planowania miesięcy: kopiowanie planów, sugestie z historii, wydatki cykliczne i lepszy kontekst roczny.

Możliwe przyszłe integracje:

- Home Assistant,
- scenariusze smart home,
- automatyzacje i powiadomienia,
- OCR/paragony,
- bezpieczny dostęp do plików.
