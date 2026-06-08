---
project: "Household Budget Mate"
version: 1
status: draft
created: 2026-05-25
updated: 2026-06-07
prd_version: 1
main_goal: quality
top_blocker: F-02 external evidence before real household data
---

# Roadmap: Household Budget Mate

> Derived from `context/foundation/prd.md` (v1) + auto-researched codebase baseline.
> Edit-in-place; archive when superseded.
> Slices below are listed in dependency order. The "At a glance" table is the index.

## Vision recap

Household Budget Mate ma daÄ‡ czĹ‚onkom jednego gospodarstwa wspĂłlny, bieĹĽÄ…cy obraz miesiÄ…ca zamiast rÄ™cznego skĹ‚adania planu i wydatkĂłw z kilku miejsc. Po decyzji z 2026-05-29 MVP nie dodaje osobnego `Safe-to-spend`; podstawowa obietnica na ten etap to moĹĽliwoĹ›Ä‡ zaplanowania miesiÄ…ca, zapisania rzeczywistych i nieoczekiwanych wydatkĂłw, zobaczenia `Live balance`, `PozostaĹ‚o w planie`, kontekstu oszczÄ™dnoĹ›ci oraz przejĹ›cia cyklu zamkniÄ™cia i ponownego otwarcia miesiÄ…ca.

## North star

W tej roadmapie **North star** oznacza najmniejszy kompletny rezultat widoczny dla uĹĽytkownika, ktĂłrego poprawne dziaĹ‚anie pokazuje, ĹĽe gĹ‚Ăłwna obietnica produktu jest speĹ‚niona.

**S-02: Domownik prowadzi miesiÄ…c i widzi wiarygodny stan miesiÄ…ca** - ten rezultat jest obecnym north star MVP: PIN, plan miesiÄ…ca, wydatki planowane/rzeczywiste/nieoczekiwane, `Live balance`, `PozostaĹ‚o w planie`, oszczÄ™dnoĹ›ci oraz `close -> reopen -> edit -> close`.

## At a glance

| ID   | Change ID                         | Outcome (user can ...)                                                                                         | Prerequisites    | PRD refs                                      | Status   |
| ---- | --------------------------------- | -------------------------------------------------------------------------------------------------------------- | ---------------- | --------------------------------------------- | -------- |
| F-01 | align-safe-to-spend-contract      | (foundation) historyczny kontrakt kwoty zostaĹ‚ superseded przez decyzjÄ™: brak osobnego Safe-to-spend w MVP      | â€”                | FR-007, US-01, Business Logic                 | superseded |
| F-02 | secure-real-data-readiness        | (foundation) warunki trwaĹ‚ego i obserwowalnego uĹĽycia danych gospodarstwa sÄ… uzgodnione i sprawdzalne          | â€”                | Non-Functional Requirements, Access Control   | done     |
| S-01 | verify-pin-gated-household-access | administrator moĹĽe utworzyÄ‡ profile PIN, a domownik moĹĽe odblokowaÄ‡ dane budĹĽetu                               | â€”                | FR-008, FR-009, US-01                         | done     |
| S-02 | verify-monthly-safe-to-spend-loop | domownik moĹĽe prowadziÄ‡ plan miesiÄ…ca, zapisaÄ‡ wydatki i zobaczyÄ‡ wiarygodny stan miesiÄ…ca                      | F-01, F-02, S-01 | US-01, FR-001, FR-002, FR-005, FR-006, FR-007 | done |
| S-03 | improve-monthly-planning          | domownik moĹĽe szybciej przygotowaÄ‡ plan miesiÄ…ca z kopii, historii, cyklicznych wydatkĂłw i sugestii rocznych   | S-02             | FR-003, FR-004, FR-007, Business Logic        | done |

## Streams

Navigation aid - grupuje elementy dzielÄ…ce Ĺ‚aĹ„cuch zaleĹĽnoĹ›ci. Kanoniczna kolejnoĹ›Ä‡ pozostaje w grafie zaleĹĽnoĹ›ci poniĹĽej; tabela pokazuje wygodnÄ… kolejnoĹ›Ä‡ czytania rĂłwnolegĹ‚ych torĂłw.

| Stream | Theme                          | Chain           | Note                                                                                                                        |
| ------ | ------------------------------ | --------------- | --------------------------------------------------------------------------------------------------------------------------- |
| A      | PoprawnoĹ›Ä‡ miesiÄ™cznego obrazu | `F-01` â†’ `S-02` â†’ `S-03` | S-03 rozwija ukoĹ„czonÄ… pÄ™tlÄ™ miesiÄ…ca o szybsze planowanie z kopii, historii, cyklicznych wydatkĂłw i rocznego kontekstu. |
| B      | Bezpieczny dostÄ™p              | `S-01`          | UkoĹ„czony; dostÄ™p PIN, fail-closed scope i lokalne odzyskiwanie zostaĹ‚y zaimplementowane i zweryfikowane.                    |
| C      | GotowoĹ›Ä‡ rzeczywistych danych  | `F-02`          | Dostarcza warunki sprawdzenia `S-02` bez ryzyka uznania Ĺ›rodowiska demonstracyjnego za docelowe.                            |

## Baseline

Stan kodu na `2026-05-25` (automatycznie zbadany i potwierdzony przez uĹĽytkownika). Przygotowania poniĹĽej nie budujÄ… ponownie obecnych warstw; dotyczÄ… rozstrzygniÄ™Ä‡ i dowodĂłw gotowoĹ›ci.

- **Frontend:** present - sÄ… strony i komponenty obsĹ‚ugujÄ…ce plan miesiÄ…ca oraz logowanie (`src/HouseholdBudgetMate.Web/Components/Pages/PlanPage/PlanPage.razor:1`, `src/HouseholdBudgetMate.Web/Components/Dialogs/UserLoginDialog.razor:106`).
- **Backend / API:** present - warstwa aplikacyjna rejestruje i wykorzystuje serwisy wydatkĂłw, kont, dochodĂłw, uĹĽytkownikĂłw oraz audytu (`src/HouseholdBudgetMate.Web/Program.cs:165`, `src/HouseholdBudgetMate.Application/Services/ExpenseService.cs:45`).
- **Data:** present - kontekst danych i migracje obejmujÄ… plany miesiÄ™cy, wydatki i uĹĽytkownikĂłw (`src/HouseholdBudgetMate.Migrations/ApplicationDbContext.cs:16`, `src/HouseholdBudgetMate.Migrations/Migrations/20260407061343_Initial.cs:72`).
- **Auth:** present - logowanie profilem PIN, sesja uĹĽytkownika i hashowanie PIN sÄ… obecne (`src/HouseholdBudgetMate.Web/Services/UserSessionService.cs:83`, `src/HouseholdBudgetMate.Application/Security/PinHasher.cs:20`).
- **Deploy / infra:** partial - istniejÄ… kontener, deklaracja Ĺ›rodowiska docelowego i plan wdroĹĽenia, ale uruchomienie docelowych zasobĂłw oraz decyzja o trwaĹ‚ym przechowywaniu danych wymagajÄ… domkniÄ™cia (`Dockerfile:1`, `render.yaml:1`, `context/foundation/deploy-plan.md:27`).
- **Observability:** partial - aplikacja ma logowanie oraz audyt zmian, ale brak potwierdzonego monitoringu dziaĹ‚ania Ĺ›rodowiska docelowego (`src/HouseholdBudgetMate.Application/Auditing/AuditSaveChangesInterceptor.cs:11`).

## Foundations

### F-01: Uzgodnienie kontraktu kwoty dostÄ™pnej do wydania lub oszczÄ™dzenia

- **Outcome:** historyczny kontrakt kwoty zostaĹ‚ superseded przez decyzje produktu z 2026-05-29/2026-05-30: MVP nie zawiera osobnego `Safe-to-spend`.
- **Change ID:** align-safe-to-spend-contract
- **PRD refs:** FR-007, US-01, Business Logic
- **Unlocks:** S-02; aktualnym rozstrzygniÄ™ciem jest model `Live balance` + `PozostaĹ‚o w planie` + kontekst oszczÄ™dnoĹ›ci.
- **Prerequisites:** â€”
- **Parallel with:** F-02, S-01
- **Blockers:** â€”
- **Unknowns:** â€”
- **Risk:** Stare dokumenty lub nazwy zmian mogÄ… sugerowaÄ‡, ĹĽe osobny wynik `Safe-to-spend` nadal jest wymagany; PRD i S-02 sÄ… aktualnym ĹşrĂłdĹ‚em prawdy.
- **Status:** superseded

### F-02: Warunki bezpiecznego uĹĽycia rzeczywistych danych

- **Outcome:** (foundation) warunki trwaĹ‚ego i obserwowalnego uĹĽycia danych gospodarstwa sÄ… uzgodnione i sprawdzalne.
- **Change ID:** secure-real-data-readiness
- **PRD refs:** Non-Functional Requirements, Access Control
- **Unlocks:** S-02 w prĂłbie z rzeczywistymi danymi; Ĺ›cieĹĽka sprawdzenia ochrony danych i ciÄ…gĹ‚oĹ›ci dziaĹ‚ania aplikacji.
- **Prerequisites:** â€”
- **Parallel with:** F-01, S-01
- **Blockers:** â€”
- **Unknowns:**
  - Jakie docelowe warunki przechowywania danych oraz kontroli dziaĹ‚ania muszÄ… byÄ‡ speĹ‚nione przed wpisaniem rzeczywistych danych gospodarstwa? â€” Owner: uĹĽytkownik. Block: no; odpowiedĹş jest wynikiem tego elementu.
- **Risk:** Bez tej bramki dziaĹ‚ajÄ…cy przepĹ‚yw uĹĽytkownika moĹĽe zostaÄ‡ uznany za gotowy do zaufanego uĹĽycia mimo ryzyka utraty danych lub niewykrytych awarii.
- **Status:** done

## Slices

### S-01: DostÄ™p gospodarstwa zabezpieczony PIN

- **Outcome:** administrator moĹĽe utworzyÄ‡ profile PIN, a domownik moĹĽe odblokowaÄ‡ dane budĹĽetu.
- **Change ID:** verify-pin-gated-household-access
- **PRD refs:** FR-008, FR-009, US-01
- **Prerequisites:** â€”
- **Parallel with:** F-01, F-02
- **Blockers:** â€”
- **Unknowns:** â€”
- **Risk:** IstniejÄ…cy kod dostÄ™pu trzeba potwierdziÄ‡ wzglÄ™dem wymagaĹ„ PRD, poniewaĹĽ peĹ‚na pÄ™tla miesiÄ…ca nie jest wiarygodna, jeĹĽeli dane mogÄ… byÄ‡ ujawnione przed odblokowaniem profilu.
- **Status:** done

### S-02: Wiarygodna pÄ™tla prowadzenia miesiÄ…ca

- **Outcome:** domownik moĹĽe prowadziÄ‡ plan miesiÄ…ca, zapisaÄ‡ wydatki i zobaczyÄ‡ wiarygodny stan miesiÄ…ca: `Live balance`, `PozostaĹ‚o w planie`, oszczÄ™dnoĹ›ci i status zamkniÄ™cia.
- **Change ID:** verify-monthly-safe-to-spend-loop
- **PRD refs:** US-01, FR-001, FR-002, FR-005, FR-006, FR-007
- **Prerequisites:** F-01, F-02, S-01
- **Parallel with:** â€”
- **Blockers:** brak dla kontrolowanego zakresu S-02; zewnÄ™trzne evidence F-02 nadal blokuje real household data.
- **Unknowns:** â€”
- **Risk:** Realne dane gospodarstwa nadal wymagajÄ… evidence z F-02; kontrolowany przepĹ‚yw S-02 jest zaakceptowany.
- **Progress:** fazy 1-5 zweryfikowane i zaakceptowane; `MonthlyBudgetingLoopTests`: 3/3, `MonthlyBudgetingLoopUiTests`: 3/3, peĹ‚ny test suite: 306/306; PRD/FR-007 przepisane bez osobnego `Safe-to-spend`.
- **Status:** done

### S-03: Usprawnienia planowania miesiÄ™cy

- **Outcome:** domownik moĹĽe szybciej przygotowaÄ‡ plan miesiÄ…ca dziÄ™ki kopiowaniu planĂłw, sugestiom z historii, aktywnym wydatkom cyklicznym, alertom przygotowanym pod przyszĹ‚e notyfikacje oraz rocznemu kontekstowi wpĹ‚ywĂłw i oszczÄ™dnoĹ›ci.
- **Change ID:** improve-monthly-planning
- **PRD refs:** FR-003, FR-004, FR-007, Business Logic
- **Prerequisites:** S-02
- **Parallel with:** â€”
- **Blockers:** â€”
- **Unknowns:**
  - Jakie typy pozycji z poprzedniego roku powinny byÄ‡ sugerowane jako sezonowe lub powtarzalne mimo braku flagi cyklicznoĹ›ci, a jakie majÄ… zostaÄ‡ pominiÄ™te jako jednorazowe? â€” Owner: uĹĽytkownik. Block: no; plan powinien zaproponowaÄ‡ reguĹ‚y startowe i moĹĽliwoĹ›Ä‡ odrzucenia sugestii.
  - Jak zaokrÄ…glaÄ‡ sugerowanÄ… kwotÄ™ po zastosowaniu bufora, np. `wydano + 10%`, aby maĹ‚e kwoty nie byĹ‚y przeszacowane, a duĹĽe kwoty nie dawaĹ‚y faĹ‚szywej precyzji? â€” Owner: implementacja. Block: no.
  - KtĂłre kategorie kwalifikujÄ… siÄ™ do alertĂłw odchylenia od Ĺ›redniej historycznej, a ktĂłre majÄ… byÄ‡ wyĹ‚Ä…czone, np. budowa domu albo inne kategorie celowo nieregularne? â€” Owner: uĹĽytkownik. Block: no.
- **Scope notes:**
  - UkoĹ„czyÄ‡ lub zweryfikowaÄ‡ istniejÄ…cy szkielet `_isCopyMode` w `PlanPage`, tak aby plan miesiÄ…ca daĹ‚o siÄ™ skopiowaÄ‡ do innego miesiÄ…ca, np. lipiec 2024 -> lipiec 2025.
  - Przy tworzeniu nowego planu zapytaÄ‡, ktĂłre pozycje z tego samego miesiÄ…ca poprzedniego roku skopiowaÄ‡ oprĂłcz aktywnych pozycji cyklicznych; sugestie powinny wykorzystywaÄ‡ podobieĹ„stwo nazw i chroniÄ‡ przed oczywistymi duplikatami.
  - DodaÄ‡ smart reguĹ‚y sugerowania kwot, bazujÄ…ce na wydanej kwocie, buforze oraz zaokrÄ…gleniu do dziesiÄ…tek albo setek zaleĹĽnie od skali pozycji.
  - DodaÄ‡ sugestie planu na podstawie historycznych Ĺ›rednich z ostatnich 3 miesiÄ™cy danej kategorii.
  - PrzygotowaÄ‡ alerty odchylenia, gdy kategoria przekracza historycznÄ… Ĺ›redniÄ… o wiÄ™cej niĹĽ 20%, tylko jako fundament pod przyszĹ‚e notyfikacje bez wysyĹ‚ania realnych powiadomieĹ„ w tym slice.
  - Automatycznie sugerowaÄ‡ aktywne wydatki cykliczne do nowego planu, jeĹ›li nie zostaĹ‚y jeszcze dodane.
  - DodaÄ‡ moĹĽliwoĹ›Ä‡ zaplanowania przewidywanych rocznych wpĹ‚ywĂłw i oszczÄ™dnoĹ›ci w sekcji `Plan roczny` w statystykach.
- **Risk:** Algorytm moĹĽe zbudowaÄ‡ plan, ktĂłremu uĹĽytkownik nie ufa, jeĹ›li bÄ™dzie mieszaĹ‚ sezonowe koszty z jednorazowymi wydatkami lub powielaĹ‚ cykliczne pozycje; slice musi traktowaÄ‡ sugestie jako propozycje do zatwierdzenia, nie jako ciche automatyczne dodawanie.
- **Status:** done

## Backlog Handoff

| Roadmap ID | Change ID                         | Suggested issue title                                         | Ready for `/10x-plan` | Notes                                                                                           |
| ---------- | --------------------------------- | ------------------------------------------------------------- | --------------------- | ----------------------------------------------------------------------------------------------- |
| F-01       | align-safe-to-spend-contract      | UzgodniÄ‡ kontrakt kwoty dostÄ™pnej do wydania lub oszczÄ™dzenia | no                    | Superseded dla MVP przez decyzjÄ™: brak osobnego `Safe-to-spend`; PRD/FR-007 przepisane.          |
| F-02       | secure-real-data-readiness        | UstaliÄ‡ warunki bezpiecznego uĹĽycia rzeczywistych danych      | no                    | UkoĹ„czone; implementacja commitem bieĹĽÄ…cym. Real-data sign-off nadal wymaga evidence z `readiness-evidence.md`. |
| S-01       | verify-pin-gated-household-access | PotwierdziÄ‡ dostÄ™p gospodarstwa zabezpieczony PIN             | no                    | UkoĹ„czone; implementacja: `5f75f53`, `fd579c3`, `3422512`, `879a267`.                            |
| S-02       | verify-monthly-safe-to-spend-loop | PotwierdziÄ‡ wiarygodnÄ… pÄ™tlÄ™ prowadzenia miesiÄ…ca             | no                    | UkoĹ„czone dla kontrolowanego no-Safe-to-spend scope; real-data gate pozostaje w F-02 evidence. |
| S-03       | improve-monthly-planning          | UsprawniÄ‡ planowanie miesiÄ™cy kopiami, historiÄ… i sugestiami  | no                    | UkoĹ„czone i zarchiwizowane; pozostaĹ‚e prace to opcjonalny manual smoke w aplikacji. |

## Open Roadmap Questions

PoniĹĽsze pytania wynikajÄ… z rĂłĹĽnicy miÄ™dzy zakresem PRD, aktualnÄ… decyzjÄ… produktowÄ… i dokumentami wdroĹĽenia.

1. **Jakie warunki trwaĹ‚oĹ›ci danych i kontroli dziaĹ‚ania muszÄ… zostaÄ‡ speĹ‚nione przed uĹĽyciem aplikacji na rzeczywistych danych gospodarstwa?** â€” Owner: uĹĽytkownik. Block: real-data MVP pilot.

## Parked

- **OCR i odczyt paragonĂłw.** â€” Why parked: PRD Â§Non-Goals odkĹ‚ada tÄ™ funkcjÄ™ do czasu sprawdzenia rÄ™cznego prowadzenia miesiÄ…ca.
- **Publiczna powierzchnia Web API.** â€” Why parked: PRD Â§Non-Goals umieszcza jÄ… poza pierwszym zakresem produktu.

## In Progress

- **F-02 external evidence before real household data.** â€” Kodowe i automatyczne readiness checks sÄ… gotowe, ale real-data pilot wymaga jeszcze `pg_dump`, restore smoke test, live `/health/ready`, Render workspace/blueprint check i manual review admin readiness panelu.

## Done

- **S-03: domownik może szybciej przygotować plan miesiąca dzięki kopiowaniu planów, sugestiom z historii, aktywnym wydatkom cyklicznym, alertom przygotowanym pod przyszłe notyfikacje oraz rocznemu kontekstowi wpływów i oszczędności.** — Archived 2026-06-07 → `context/archive/2026-06-03-improve-monthly-planning/`. Lesson: —.
- **F-02: warunki trwałego i obserwowalnego użycia danych gospodarstwa są uzgodnione i sprawdzalne.** — Archived 2026-06-03 → `context/archive/2026-05-27-secure-real-data-readiness/`. Lesson: —.
- **F-01: historyczny kontrakt Safe-to-spend.** — Archived 2026-06-03 → `context/archive/2026-05-26-align-safe-to-spend-contract/`. Lesson: —.
- **S-02: wiarygodna pętla prowadzenia miesiąca.** — Archived 2026-06-03 → `context/archive/2026-05-29-verify-monthly-safe-to-spend-loop/`. Lesson: —.
- **S-01: administrator może utworzyć profile PIN, a domownik może odblokować dane budżetu.** — Archived 2026-06-03 → `context/archive/2026-05-27-verify-pin-gated-household-access/`. Lesson: —.

- **F-01: historyczny kontrakt Safe-to-spend.** â€” UkoĹ„czone 2026-05-27, ale superseded dla MVP przez decyzje z 2026-05-29/2026-05-30: aplikacja nie bÄ™dzie miaĹ‚a osobnego `Safe-to-spend`. PRD/FR-007 i S-02 sÄ… aktualnym ĹşrĂłdĹ‚em prawdy.
- **F-02: (foundation) warunki trwaĹ‚ego i obserwowalnego uĹĽycia danych gospodarstwa sÄ… uzgodnione i sprawdzalne.** â€” UkoĹ„czone 2026-05-28; dodano readiness contract, `/health/ready`, blokadÄ™ publicznego `/files`, utwardzenie cookie, retencjÄ™ logĂłw, admin readiness panel oraz evidence log. Real-data sign-off pozostaje zaleĹĽny od uzupeĹ‚nienia `context/changes/secure-real-data-readiness/readiness-evidence.md` przed wpisaniem rzeczywistych danych.
- **S-01: administrator moĹĽe utworzyÄ‡ profile PIN, a domownik moĹĽe odblokowaÄ‡ dane budĹĽetu.** â€” UkoĹ„czone 2026-05-27; implementacja: `5f75f53`, `fd579c3`, `3422512`, `879a267`. Zmiana pozostaje poza archiwum zgodnie z decyzjÄ… o niecommitowaniu `context/`.
- **S-02: wiarygodna pÄ™tla prowadzenia miesiÄ…ca.** â€” UkoĹ„czone 2026-05-30 dla kontrolowanego zakresu MVP bez osobnego `Safe-to-spend`; testy usĹ‚ug i kontraktu UI przechodzÄ…, a uĹĽytkownik potwierdziĹ‚ finalny sign-off.

