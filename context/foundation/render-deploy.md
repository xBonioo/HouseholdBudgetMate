# Deploy na Render

Ten projekt ma dwa tryby uruchamiania:

- Windows/MSI/lokalny LAN: zostaje bez zmian, dalej używa `https://localhost:5001`, `http://localhost:5000` i lokalnego `config.json`.
- Docker/Render: używa zmiennych środowiskowych i publicznego HTTPS zakończonego przez Render.

## Lokalny test Dockera

```powershell
docker compose up --build
```

Po starcie aplikacja będzie dostępna lokalnie pod:

```text
http://localhost:10000
```

PostgreSQL działa w kontenerze `postgres`, a dane są trzymane w wolumenie `postgres-data`.

Przy pierwszym wejściu lokalnie w Dockerze aplikacja pokaże `/setup`. Wpisz:

```text
Host: postgres
Port: 5432
Login: household_budget_mate
Hasło: household_budget_mate
Nazwa bazy: household_budget_mate
```

## Render Blueprint

Plik `render.yaml` tworzy:

- usługę web Docker: `household-budget-mate-web`,
- bazę PostgreSQL: `household-budget-mate-db`,
- zmienną `DATABASE_URL` pobieraną z bazy Render.

Render automatycznie wystawia publiczne HTTPS na domenie `*.onrender.com`. Kontener aplikacji powinien słuchać po HTTP na `0.0.0.0:$PORT`. Aplikacja obsługuje to automatycznie.

## Ważne zmienne środowiskowe

```text
DATABASE_URL
Application__MigrateDatabaseOnStart=true
Application__SeedDataToDatabase=false
HOUSEHOLDBUDGETMATE_CONTAINER=true
HOUSEHOLDBUDGETMATE_DATA_DIR=/var/lib/householdbudgetmate
```

`DATABASE_URL` może być w formacie Render/Postgres:

```text
postgresql://user:password@host:port/database
```

Aplikacja konwertuje go na connection string zgodny z Npgsql.

## HTTPS na Render

W kontenerze aplikacja nie uruchamia własnego HTTPS. Render kończy TLS na swoim load balancerze i przekazuje ruch do kontenera po HTTP. Aplikacja używa nagłówków `X-Forwarded-Proto`, żeby poprawnie rozpoznawać oryginalny schemat HTTPS.

## Pliki i trwałość danych

Dane biznesowe są w PostgreSQL. Katalog `HOUSEHOLDBUDGETMATE_DATA_DIR` służy na pliki aplikacji, lokalny config i certyfikaty. Na Render bez persistent disk ten katalog jest efemeryczny. Jeżeli uploadowane pliki mają być trwałe, dodaj w Render persistent disk zamontowany pod:

```text
/var/lib/householdbudgetmate
```

W zakresie MVP publiczne serwowanie `/files` ma pozostać wyłączone lub zablokowane. OCR i upload plików są poza MVP; przyszła zmiana OCR musi dodać autoryzowany dostęp do plików zamiast ponownie wystawiać katalog aplikacji publicznie.

Flaga `FileStorage__EnablePublicFileServing` domyślnie ma wartość `false`; ustawienie jej na `true` jest poza granicą gotowości real-data MVP.
Flaga `Blazor__DetailedErrors` domyślnie ma wartość `false` dla Render/Production, żeby szczegóły wyjątków serwerowych nie wychodziły przez obwody Blazor.

## Real-data MVP pilot

Docelowy Render Blueprint nadal używa darmowych planów Render dla MVP. To jest świadomie zaakceptowany pilot z ryzykiem, nie pełna trwała produkcja.

Przed wpisaniem realnych danych gospodarstwa:

1. Uzupełnij `context/changes/secure-real-data-readiness/readiness-evidence.md`.
2. Wykonaj lokalny `pg_dump` z zewnętrznego URL bazy Render.
3. Odtwórz dump do nieprodukcyjnej bazy PostgreSQL i zapisz wynik smoke testu.
4. Sprawdź `/health/ready` po deployu.
5. Przed każdą migracją na realnych danych zapisz przegląd migracji, świeży backup i notatki rollback/forward-fix.

## Pierwsze logowanie

Lokalnie w Dockerze aplikacja przechodzi przez `/setup`, bo baza działa w osobnym kontenerze i dane połączenia zapisują się do `config.json` w wolumenie `web-data`.

Przy deployu na Render aplikacja używa `DATABASE_URL`, więc nie przechodzi przez lokalny `/setup`. Migracje uruchamiają się przy starcie. Techniczny właściciel `default-user` pozostaje wewnętrznym właścicielem wspólnego budżetu, ale nie jest profilem interaktywnym i nie może służyć do logowania bez PIN-u.
