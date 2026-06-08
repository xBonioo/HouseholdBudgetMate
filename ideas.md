Aplikacja do zarządzania budżetem - Household Budget Mate

- hosted on my server at home (deploy na serwer)

- działa w LAN

- .NET 10

- mobile przez przeglądarkę

- możliwość rozwoju
1. CORE STACK (fundament)
   
   - Backend + Frontend
       ASP.NET Core (.NET 10)
       Blazor Server (SSR + interactivity)
     
       Web Api
   - Baza danych
       PostgreSQL
       Entity Framework Core
   - ORM / Data
       EF Core
     
       Dapper
       Migrations
   - Chciałbym aby aplikacja działało jako exe, który się odpala i jest web w przeglądarce lub mobile przez przeglądarke, więc:
     - Przy pierwszym uruchomieniu:
       - otwiera się /setup
       - user wpisuje:
           connection string
         zapisujesz do:
         pliku np. config.json obok exe

2. UI / FRONTEND
   
   - UI
       Blazor Components
       Razor Pages
   - Styling
       MudBlazor
       Tailwind (możliwe jeśli będzie potrzebne)
   - Wykresy
       Chart.js (przez JSInterop)
   - Multi language
       Polski i angielski, ale chciałbym aby użytkownicy githuba mogli dodawać swoje języki do projektu (resources)

3. HOSTING (hosted on my server at home (deploy na serwer))
   
   - Lokalny serwer
       Na razie laptop w przyszłości serwer w domu
   - Dostęp
       LAN: http://192.168.x.x:5001

4. SECURITY
- Auth
  
  - PIN (4-8 cyfr) zapisywany hash w bazie
5. Architektura warstowa
   - Abstractions
   - Application
   - Domain
   - Installer
   - Migrations
   - Tests
   - Tray
   - Web
   - WebApi

5.1 1. 
    - Event-driven (lekko)
        statystyk
        notyfikacji
        OCR processing (in future)
        - np:
            ExpenseCreatedEvent
            ReceiptScannedEvent
            BudgetExceededEvent
    - Background jobs (np: Hangfire / Quartz.NET)
        - OCR
        - cykliczne wydatki
        - statystyki

6. OCR (in future)
    Tesseract OCR
    (backend processing zdjęć)

7. Funkcjonalność:
   
   - Planowanie wydatków dla każdego miesiąca 
     - każdy miesiąc powinien móc mieć około 50 pozycji wydatków (oczywiście chciałbym aby to było dynamicznie, bo jeden miesiąc może mieć 15, a inny 50 czy 35)
     - możliwość dodania nie planowanych wydatków
     - możliwość ustawienie budżetu „envelope” dla niektórych kategorii
       Envelope to miesięczny limit wydatków dla kategorii. Przekroczenie nie blokuje dodania wydatku, ale UI sygnalizuje przekroczenie. Limit nie przenosi się na kolejny miesiąc
     - Wydatki cykliczne (abonamenty, kredyt etc)
       Przy zamknięciu/otwarciu miesiąca system auto-generuje pozycje planu dla wszystkich aktywnych wydatków cyklicznych. Generowanie idempotentne — podwójne wywołanie nie duplikuje rekordów.
   - Wydatki:
     - tutaj powinny być pola dla nazwy, kategorii (zdefiniowane w aplikacji), planowana kwota, realna wydana kwota, pole czy UI ma uwzglednić czy pokazywać ilość pozostałej kwoty czy nie
     - dodawanie wydatku
     - edycja wydatku
     - usuwanie wydatku
     - do niektórcch wydatków chciałbym dodawać pozycje, na przykład paliwo chciałbym dodać tam kilka wierszy z tankowań do realnej wydanej kwoty, suplementy w jakich aptekach i ile wydałem etc
   - Kategorie
     - lista kategorii
     - dodawanie kategorii
     - edycja kategorii
     - usuwanie kategorii
   - Tagi (obok kategorii)
     - Chodzi o to, że każda kategoeria będzie mogła mieć swoje tagi i w tych tagach bedą podtagi np Spożywcze to sklepy, internetowe to allegro, aliexpress etc
   - Dashboard
     - suma wydatków w bieżącym miesiącu
     - liczba transakcji
     - lista rozplanowanych wydatków:
       - suma kwotowa planów
       - ile zostało już wydane
       - ile pozostało
     - Wyszukiwarka + filtry
   - Konta
     - dodawanie konta:
       - gotówka
       - bank
       - inne
     - saldo konta na koniec miesiąca i w nowym miesiącu saldo kont łączy się z wpływami
       Saldo bieżące = suma sald wszystkich kont + suma wpływów w miesiącu − suma realnych wydatków w miesiącu. Saldo jest live
   - Wpływy
     - dodawanie przychodów
     - możliwość wprowadzenia którego dnia wpływa wypłata, kiedy dostaje jakieś dodatki
   - Wykresy
     - wydatki per kategoria (pie chart)
     - wydatki per miesiąc (bar chart)
   - Analiza
     - porównanie miesiąc do miesiąca
     - trendy wydatków
   - Statystyki
     - średnie wydatki dla kategorii, sklepu, roku
     - największe kategorie
     - historia roczna
     - możliwość zaplanowania podejrzewanych rocznych wpływów, wydatków i oszczędności 
     - średnie miesięczne wpływy, wydatki i oszczędności 
     - wprowadzenie ubiegłorocznych średnich statystyk z kategorii kredytu, wydatków na budowe domu etc
     - medianę (nie tylko średnią)
     - odchylenie
     - sezonowość
   - UX mobilny (KLUCZOWE)
     - szybkie dodanie wydatku (1 ekran max)
     - duże przyciski
     - “quick add” (kwota + kategoria + enter)
   - Historii zmian (audit trail)
   - Dostep spoza domu (VPN)
   - Portfele inwestycyjne
   - Zarządzanie kosztami swojego JDG
   - Cele finansowe
   - Alerty / powiadomienia
   - Prognozowanie na bazie historii:
   - np:
     - “wydasz ~3200 zł w tym miesiącu”
     - “za 3 miesiące saldo = X”
- Multi-user / household
- Multi-currency
- Multi language
- Backup'y
- Api do getów aby móc sparować apke z home assist



### Użytkownicy, role i tryb gospodarstwa domowego

Aplikacja obsługuje wielu użytkowników. Istnieje konto domyślne/root, które nie wymaga PIN-u i zawsze posiada uprawnienia administratora. Pozostali użytkownicy mogą mieć PIN o długości 4-8 cyfr, przechowywany w bazie jako bezpieczny hash.

System posiada role administracyjne. Tylko administratorzy widzą i mogą otworzyć sekcję administracyjną. Administrator może tworzyć użytkowników, zmieniać ich PIN-y, nadawać lub odbierać uprawnienia administratora oraz konfigurować sposób współdzielenia budżetu.

Obsługiwane są dwa tryby pracy:

- osobny budżet dla użytkownika,
- wspólny budżet z innym użytkownikiem/gospodarstwem.

Dane finansowe są przypisywane do właściciela budżetu (`UserId` / `BudgetOwnerUserId`), dzięki czemu użytkownicy mogą mieć oddzielne budżety albo współdzielić wybrane dane.

### Logowanie i sesja użytkownika

Po wejściu do aplikacji użytkownik wybiera profil i wpisuje PIN w oknie dialogowym z klawiaturą numeryczną. Sesja użytkownika jest zapisywana w cookie, więc aplikacja może przywrócić ostatnio wybranego użytkownika po odświeżeniu strony.

Konto domyślne/root nie wymaga PIN-u i służy jako globalny administrator.

### Administracja aplikacją

Sekcja administracyjna pozwala zarządzać użytkownikami i konfiguracją aplikacji w czasie działania systemu. UI pokazuje, którzy użytkownicy mają uprawnienia administratora, jaki mają tryb budżetu oraz czy korzystają z PIN-u.

Konfiguracja połączenia z bazą danych jest obsługiwana przez UI. Hasło do bazy jest domyślnie ukryte, z możliwością tymczasowego pokazania przez ikonę widoczności.

### Plan miesiąca

Plan miesiąca został rozwinięty w pełny moduł operacyjny. Obsługuje:

- dynamiczną listę wydatków,
- wydatki planowane i nieplanowane,
- kwoty planowane i rzeczywiste,
- limity envelope dla kategorii,
- ostrzeżenia po przekroczeniu limitu,
- KPI miesiąca,
- zamykanie i otwieranie miesięcy,
- kopiowanie pozycji do kolejnego miesiąca,
- automatyczne dodawanie cyklicznych wydatków,
- automatyczne dodawanie cyklicznych wpływów,
- pozycje oszczędnościowe/przelewy oszczędnościowe.

Generowanie cyklicznych pozycji jest idempotentne, więc ponowne uruchomienie synchronizacji nie powinno duplikować danych.

### Wydatki i pozycje wydatków

Wydatki obsługują kategorie, tagi i podtagi. Można dodawać, edytować i usuwać wydatki oraz rozbijać je na pozycje szczegółowe, np. kilka tankowań, zakupy w różnych sklepach albo kilka pozycji z jednej kategorii.

System obsługuje wyszukiwanie historii wydatków, filtrowanie po zakresie dat i przechodzenie z wyników wyszukiwania do edycji konkretnego wydatku.

### Kategorie, tagi i podtagi

Kategorie i tagi są wspólne dla aplikacji, a nie przypisane do konkretnego użytkownika. Kategorie mogą mieć tagi, a tagi mogą mieć strukturę podtagów. Długie nazwy kategorii i tagów są obsługiwane w UI tak, aby nie psuły układu formularzy.

### Konta i saldo bieżące

Aplikacja obsługuje konta typu gotówka, bank i inne. Można prowadzić salda miesięczne kont oraz liczyć saldo bieżące na podstawie:

- sald kont,
- wpływów w miesiącu,
- rzeczywistych wydatków.

Saldo jest wykorzystywane w widokach dashboardu i bieżącej kontroli budżetu.

### Wpływy

Wpływy mogą być dodawane ręcznie lub jako definicje cykliczne. Cykliczne wpływy mogą być automatycznie synchronizowane z wybranym miesiącem, podobnie jak cykliczne wydatki.

### Kredyty

Kredyty zostały rozwinięte jako osobny moduł, a nie tylko jako zwykły wydatek cykliczny. Moduł obsługuje:

- kredyty przypisane do użytkownika/budżetu,
- harmonogram rat,
- oznaczanie rat jako zapłacone,
- zmiany oprocentowania,
- dodatkowe opłaty,
- powiązanie rat kredytu z planem miesiąca,
- współdzielenie kredytu w ramach wspólnego budżetu.

Dzięki temu kredyt może być zarządzany razem przez gospodarstwo domowe, nawet jeśli część budżetu użytkownicy prowadzą osobno.

### Dashboard

Dashboard pokazuje podsumowanie bieżącego miesiąca, między innymi:

- sumę wydatków,
- liczbę transakcji,
- planowane i rzeczywiste kwoty,
- pozostałe środki,
- saldo bieżące,
- ostatnie wydatki,
- elementy związane z kategoriami i oszczędnościami.

### Statystyki

Aplikacja posiada stronę statystyk z wyszukiwarką zakupów, analizą wydatków per kategoria oraz widokami rocznymi/miesięcznymi. Obsługiwane są zakresy dat i filtrowanie wyników.

Aktualnie statystyki są bardziej raportowe niż prognostyczne. Prognozy, trendy, sezonowość, mediana i odchylenia pozostają jako osobny kierunek rozwoju.



## Założenie dla nowej wersji aplikacji

Nowa wersja aplikacji ma powstać na podstawie doświadczeń z obecnej aplikacji Household Budget Mate. Celem nie jest skopiowanie obecnej implementacji 1:1, ale zachowanie sprawdzonych funkcji, encji domenowych i sposobu myślenia o budżecie domowym, przy jednoczesnym ulepszeniu architektury, UX, wydajności, testowalności i możliwości dalszego rozwoju.

Obecna aplikacja pokazała, że najważniejsze są:

- planowanie budżetu miesięcznego,
- szybkie dodawanie i edycja wydatków,
- kategorie, tagi i podtagi,
- konta i saldo bieżące,
- wpływy zwykłe i cykliczne,
- wydatki zwykłe i cykliczne,
- kredyty jako osobny moduł,
- wielu użytkowników,
- budżet osobny lub współdzielony,
- administracja użytkownikami,
- konfiguracja bazy danych,
- lokalne uruchamianie aplikacji jako program na Windows,
- dostęp przez LAN z telefonu.

Nowa aplikacja powinna zachować te idee funkcjonalne, ale zaprojektować je czyściej i bardziej przyszłościowo.

## Funkcjonalności, które należy zachować z obecnej aplikacji

### Użytkownicy i gospodarstwo domowe

Aplikacja powinna obsługiwać wielu użytkowników. Każdy użytkownik może mieć własny PIN, zapisany w bazie jako hash. Konto główne/root powinno istnieć jako domyślny administrator.

Użytkownik może działać w jednym z trybów:

- osobny budżet,
- wspólny budżet z innym użytkownikiem lub gospodarstwem.

Dane finansowe powinny być przypisane do właściciela budżetu. Dzięki temu użytkownicy mogą prowadzić oddzielne budżety, ale wybrane obszary, np. kredyty lub wspólny budżet domowy, mogą być współdzielone.

### Role i administracja

Aplikacja powinna mieć role użytkowników, minimum:

- administrator,
- zwykły użytkownik.

Tylko administrator może zarządzać użytkownikami, PIN-ami, uprawnieniami i konfiguracją aplikacji.

Nowi użytkownicy nie powinni być administratorami domyślnie. Konto root zawsze powinno mieć uprawnienia administratora.

### Plan miesiąca

Plan miesiąca jest centralnym elementem aplikacji.

Plan powinien zawierać:

- rok,
- miesiąc,
- status otwarty/zamknięty,
- listę wydatków,
- pozycje oszczędnościowe,
- KPI miesiąca.

Plan miesiąca powinien pozwalać na:

- dodawanie wydatków planowanych,
- dodawanie wydatków nieplanowanych,
- ustawianie kwot planowanych i rzeczywistych,
- zamykanie miesiąca,
- otwieranie miesiąca,
- kopiowanie wybranych pozycji do kolejnego miesiąca,
- automatyczne dodawanie wydatków cyklicznych,
- automatyczne dodawanie wpływów cyklicznych.

Generowanie cyklicznych pozycji powinno być idempotentne, czyli ponowne uruchomienie synchronizacji nie może tworzyć duplikatów.

### Wydatki

Wydatki powinny obsługiwać:

- nazwę,
- kategorię,
- opcjonalny tag lub podtag,
- kwotę planowaną,
- kwotę rzeczywistą,
- kolejność na liście,
- informację, czy pokazywać pozostałą kwotę w UI,
- powiązanie z wydatkiem cyklicznym,
- powiązanie z ratą kredytu,
- miękkie usuwanie.

Wydatki powinny mieć możliwość dodawania pozycji szczegółowych. Przykłady:

- kilka tankowań w ramach kategorii paliwo,
- kilka zakupów w różnych sklepach,
- kilka pozycji z jednej większej kategorii.

### Pozycje wydatku

Pozycja wydatku powinna zawierać:

- opis,
- kwotę,
- datę wystąpienia,
- opcjonalny tag/podtag.

Pozycje wydatku pozwalają rozbić jeden zaplanowany wydatek na mniejsze realne transakcje.

### Kategorie

Kategorie powinny być wspólne dla aplikacji, a nie przypisane do konkretnego użytkownika.

Kategoria powinna zawierać:

- nazwę,
- kolor,
- opcjonalny limit envelope,
- informację, czy wspiera pozycje szczegółowe,
- miękkie usuwanie.

Limit envelope oznacza miesięczny limit wydatków dla kategorii. Przekroczenie limitu nie blokuje dodania wydatku, ale powinno być wyraźnie pokazane w UI.

### Tagi i podtagi

Tagi powinny należeć do kategorii. Tag może mieć tag nadrzędny, dzięki czemu można budować strukturę podtagów.

Przykład:

- Kategoria: Spożywcze
  - Tag: Lidl
  - Tag: Biedronka
  - Tag: Auchan

Albo:

- Kategoria: Internetowe
  - Tag: Allegro
  - Tag: AliExpress
  - Tag: Amazon

Tag powinien zawierać:

- nazwę,
- kategorię,
- opcjonalny tag nadrzędny,
- opcjonalne nadpisanie ustawienia, czy wspiera pozycje szczegółowe,
- miękkie usuwanie.

### Wydatki cykliczne

Aplikacja powinna obsługiwać definicje wydatków cyklicznych.

Definicja wydatku cyklicznego powinna zawierać:

- nazwę,
- kategorię,
- opcjonalny tag,
- kwotę,
- aktywność,
- kolejność,
- informację, czy pokazywać pozostałą kwotę w UI.

Wydatki cykliczne powinny być możliwe do automatycznego dodania do planu miesiąca.

### Konta

Aplikacja powinna obsługiwać konta finansowe.

Konto powinno zawierać:

- nazwę,
- typ konta,
- kolejność,
- status aktywne/zarchiwizowane.

Typy kont:

- gotówka,
- bank,
- oszczędności,
- inne.

Konto powinno mieć miesięczne saldo zamknięcia.

### Salda kont

Saldo konta powinno być przechowywane per:

- konto,
- rok,
- miesiąc.

Na podstawie sald kont, wpływów i wydatków aplikacja powinna liczyć saldo bieżące.

Saldo bieżące:

- suma sald kont,
- plus wpływy w miesiącu,
- minus rzeczywiste wydatki w miesiącu.

### Wpływy

Wpływy powinny obsługiwać:

- nazwę,
- kwotę,
- rok,
- miesiąc,
- oczekiwany dzień wpływu,
- konto,
- informację, czy wpływ pochodzi z definicji cyklicznej,
- miękkie usuwanie.

### Wpływy cykliczne

Aplikacja powinna obsługiwać definicje wpływów cyklicznych.

Definicja wpływu cyklicznego powinna zawierać:

- nazwę,
- kwotę,
- dzień miesiąca,
- konto,
- aktywność.

Wpływy cykliczne powinny być możliwe do automatycznego dodania do miesiąca.

### Kredyty

Kredyt powinien być osobnym modułem, a nie tylko zwykłym wydatkiem cyklicznym.

Kredyt powinien zawierać:

- nazwę,
- typ kredytu,
- tryb oprocentowania,
- opcjonalny typ WIBOR,
- kapitał,
- oprocentowanie,
- marżę,
- dzień spłaty,
- datę startu,
- datę końca,
- opcjonalny tag,
- status aktywny/nieaktywny,
- właściciela budżetu/użytkownika.

Typy kredytu:

- gotówkowy,
- hipoteczny.

Tryby oprocentowania:

- stałe,
- zmienne, np. WIBOR.

Kredyt powinien mieć:

- harmonogram rat,
- historię zmian stóp,
- dodatkowe opłaty,
- możliwość oznaczania rat jako zapłacone,
- możliwość powiązania rat z planem miesiąca.

### Raty kredytu

Rata kredytu powinna zawierać:

- kredyt,
- rok,
- miesiąc,
- datę płatności,
- całkowitą kwotę raty,
- część kapitałową,
- część odsetkową,
- status zapłacona/niezapłacona,
- datę zapłaty.

Rata może być powiązana z wydatkiem w planie miesiąca.

### Opłaty kredytu

Kredyt powinien obsługiwać dodatkowe opłaty.

Opłata kredytu powinna zawierać:

- nazwę,
- typ opłaty,
- częstotliwość,
- kwotę,
- datę startu,
- opcjonalną datę końca,
- aktywność.

Typy opłat:

- ubezpieczenie,
- prowizja,
- opłata,
- inne.

Częstotliwość:

- jednorazowo,
- miesięcznie,
- rocznie.

### Zmiany oprocentowania kredytu

Kredyt powinien obsługiwać historię zmian oprocentowania lub stawek referencyjnych.

Wpis zmiany oprocentowania powinien zawierać:

- kredyt,
- datę obowiązywania od,
- stawkę referencyjną.

### Oszczędności / transfery oszczędnościowe

Plan miesiąca powinien obsługiwać pozycje oszczędnościowe lub transfery oszczędnościowe.

Pozycja oszczędnościowa powinna zawierać:

- plan miesiąca,
- kwotę,
- datę transferu.

### Logi techniczne

Aplikacja powinna zachować logi techniczne dla diagnostyki.

Log powinien zawierać:

- wiadomość,
- szablon wiadomości,
- poziom,
- datę,
- wyjątek,
- właściwości.

To nie zastępuje historii zmian użytkownika. Docelowo warto dodać osobny audit trail dla działań biznesowych.

## Encje domenowe do zachowania lub przemyślenia w nowej wersji

Nowa wersja powinna zachować następujące pojęcia domenowe:

- User
- Account
- AccountMonthBalance
- Category
- Tag
- MonthPlan
- MonthSavingsTransferItem
- Expense
- ExpenseLineItem
- RegularExpenseDefinition
- Income
- RegularIncomeDefinition
- Loan
- LoanInstallment
- LoanRateEntry
- LoanCharge
- LogEntry

Nazwy mogą zostać zmienione, jeśli nowa architektura będzie tego wymagać, ale znaczenie domenowe tych encji powinno pozostać.

## Co poprawić w nowej wersji

Nowa aplikacja powinna zachować domenę i funkcje, ale poprawić:

- czytelniejszy podział modułów,
- lepsze API pomiędzy frontendem i backendem,
- lepszą obsługę mobile-first,
- bardziej spójny design system,
- prostsze formularze,
- lepszą analitykę,
- prognozy i trendy,
- wykresy,
- backupy,
- wielojęzyczność,
- integracje,
- testowalność,
- czytelniejsze reguły uprawnień,
- lepsze przygotowanie pod przyszłe OCR i automatyzacje.





8. Poza MVP:
   OCR
   - możliwość skanowania paragonów z różnych sklepów (spożywcze, ciuchy etc)
   - odczyt:
     - nazwy produkty
     - kwoty
     - daty
   - mapowanie produktów → kategorii, tag
   - agregacja paragonu do jednej transakcji