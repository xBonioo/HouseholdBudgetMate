# Artifact 3: Contributors Map

Data: analiza wykonana 2026-06-09 jako kontynuacja `context/map/artifact-1-territory.md` oraz `context/map/artifact-2-structure.md`.

Cel: dla pieciu obszarow wymagajacych potencjalnego kontaktu z kontrybutorami wskazac kluczowych ludzkich autorow z ostatnich tygodni oraz pogrupowac ich aktywnosci tematycznie tak, aby bylo jasne, kto moze zaoferowac support.

## Zakres

- Zakres historii: ostatnie tygodnie aktywnosci od 2026-05-18 do 2026-06-09.
- Analizowane obszary:
  - miesieczny loop budzetowy: `PlanPage`, `ExpenseService`, `IExpenseService`, kontrakty wydatkow i `ExpenseServiceTests`;
  - kontrakty wydatkow: DTO/requesty oraz interfejs `IExpenseService`;
  - shell/startup: `Program.cs` i `MainLayout.razor`;
  - backup/restore i admin safety: `AdminBackup.razor` oraz `backup-drop-zone.js`;
  - agregacyjne ekrany finansowe: `Accounts`, `Home`, `Statistics`, `Charts` i `charts.js`.

## Filtr Autorstwa

Odfiltrowano boty, automatyzacje oraz agentow bez wyraznego autorstwa czlowieka. W badanym zakresie wszystkie istotne commity w analizowanych obszarach maja tego samego autora i committera:

| Osoba | Email | Rola w historii |
|---|---|---|
| Kamil Swiderski | `swiderski.kamil.1998@gmail.com` | Autor i committer zmian w analizowanych obszarach |

W historii wystepuja pojedyncze wpisy `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`, ale autor i committer tych commitow to Kamil Swiderski. Na potrzeby tej mapy sa traktowane jako ludzkie commity wspierane narzedziem, a nie jako niezalezny kontrybutor.

## Kluczowi Kontrybutorzy Wedlug Obszaru

| Obszar | Kluczowy support | Tematy aktywnosci wskazujace kompetencje |
|---|---|---|
| Miesieczny loop budzetowy: `PlanPage` + `ExpenseService` | Kamil Swiderski | Monthly planning, safe-to-spend, kontrakt wynikow finansowych, poprawki `PlanPage`, e2e/debug flow, testy wydatkow, mobile UX |
| Kontrakty wydatkow: DTO/requesty + `IExpenseService` | Kamil Swiderski | `align-safe-to-spend-contract`, pola `LiveBalanceDto`, definicje finansowe, requesty/DTO wydatkow, integracja kontraktu z UI |
| Shell/startup: `Program.cs` + `MainLayout.razor` | Kamil Swiderski | PIN-gated access, multi-user/session flow, admin gate, readiness gate, cookies/theme, LAN launch, Docker/setup, globalna kompozycja aplikacji |
| Backup/restore i admin safety | Kamil Swiderski | Export backup, restore workflow, folder picker, admin safeguards, file input/drag-drop, readiness/admin flows |
| Agregacyjne ekrany finansowe: `Accounts`, `Home`, `Statistics`, charts | Kamil Swiderski | Accounts/statistics fixes, spending statistics per category, charts + dark mode, recurring/account UI, financial indicators in UI, loans/account balances |

## Aktywnosci Pogrupowane Tematycznie

### Kamil Swiderski

#### Planowanie miesieczne i logika wydatkow

- `feat(planning): improve monthly planning`
- `e2e test, debug agent and improve plan page`
- `Implements multi-user support with user scoping and PIN login, improved planPage and expenses`
- `Expands and refines test coverage and improves mobile UX`
- `fix support lines`

Wskazanie supportu: najlepszy punkt kontaktu przy zmianach w `PlanPage`, `ExpenseService`, safe-to-spend, archiwalnych miesiacach, przeplywie planowania i testach zabezpieczajacych zachowanie wydatkow.

#### Kontrakty finansowe i definicje domenowe

- `feat(align-safe-to-spend-contract): establish financial result contract (p1)`
- `feat(align-safe-to-spend-contract): present financial indicators in UI (p3)`
- `Unifies decimal parsing, modernizes recurring/account UIs and small refactor`

Wskazanie supportu: najlepszy punkt kontaktu przy zmianach DTO/requestow, pol kontraktow, definicji salda, rezerw, safe-to-spend oraz sposobu prezentowania wskaznikow finansowych w UI.

#### Dostep, setup, administracja i globalny shell

- `feat(verify-pin-gated-household-access): establish secure administrator gate (p1)`
- `feat(verify-pin-gated-household-access): complete profile recovery workflow (p3)`
- `fix(verify-pin-gated-household-access): address review findings`
- `feat(secure-real-data-readiness): add real-data readiness gate`
- `lan launch`
- `docker`

Wskazanie supportu: najlepszy punkt kontaktu przy `Program.cs`, `MainLayout.razor`, sesji uzytkownika, PIN-gated access, readiness/admin panel, konfiguracji uruchomieniowej, cookies/theme i globalnej kompozycji aplikacji.

#### Backup, restore i admin safety

- `feat(sprint-10-export-backup): add export backup and restore workflow`
- `Improve backup restore and admin safeguards`
- `folder picker`

Wskazanie supportu: najlepszy punkt kontaktu przy formacie backupu, restore workflow, integracji z plikami, drag/drop, folder pickerze, admin safeguards i ryzykach integralnosci danych.

#### Ekrany agregacyjne, wykresy i dashboardy finansowe

- `spending statistics per category`
- `small fixes in accounts and statistics pages`
- `implement charts and dark mode`
- `dark mode fix at expense`
- `loan improvements`
- `add expense mobile`

Wskazanie supportu: najlepszy punkt kontaktu przy `Accounts`, `Home`, `Statistics`, `ChartCanvas`, `charts.js`, KPI, saldach, wykresach, dark mode, mobile UX i spojnosc prezentacji danych finansowych.

## Wniosek

Repozytorium w ostatnich tygodniach nie pokazuje rozproszonego ownershipu dla analizowanych obszarow. Po odfiltrowaniu agentow, botow i automatyzacji jedynym realnym kontrybutorem widocznym w historii jest Kamil Swiderski.

Praktyczna interpretacja: przy kazdym z pieciu obszarow kontakt powinien isc do tej samej osoby, ale z innym kontekstem pytania:

- dla `PlanPage` i `ExpenseService`: pytac o semantyke miesiecznego loopa budzetowego;
- dla kontraktow wydatkow: pytac o definicje finansowe i kompatybilnosc DTO/requestow;
- dla `Program.cs` i `MainLayout`: pytac o konsekwencje globalne, sesje i setup;
- dla backup/restore: pytac o bezpieczenstwo i integralnosc danych;
- dla ekranow agregacyjnych: pytac o interpretacje KPI, sald, statystyk i wykresow.
