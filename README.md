# HouseholdBudgetMate

Prosty opis uruchomienia i budowania instalatora MSI.

## Wymagania

- Windows 10/11
- .NET SDK (zgodny z projektem)
- WiX Toolset (do budowania `HouseholdBudgetMate.Installer.wixproj`)
- PowerShell 5+

## Najważniejsze komendy

Uruchamiaj w PowerShell.

```powershell
dotnet build "F:\Kamil\.Net\_projects\HouseholdBudgetMate\src\HouseholdBudgetMate.Installer\HouseholdBudgetMate.Installer.wixproj" -c Release
```

```powershell
powershell -ExecutionPolicy Bypass -File "F:\Kamil\.Net\_projects\HouseholdBudgetMate\files\build-msi.ps1" -Configuration Release
```

## Konfiguracja startowa (skrót)

Aplikacja `HouseholdBudgetMate.Web` przy starcie:

- ładuje konfigurację runtime z katalogu użytkownika (`%APPDATA%\\HouseholdBudgetMate`),
- jeśli znajdzie stary `config.json` obok `.exe`, kopiuje go do `%APPDATA%\\HouseholdBudgetMate\\config.json`,
- uruchamia migracje bazy danych przy starcie (jeżeli konfiguracja jest kompletna),
- tworzy katalog plików użytkownika w lokalizacji zapisywalnej (nie w `Program Files`).

## Gdzie szukać ustawień

- Runtime config: `%APPDATA%\\HouseholdBudgetMate\\config.json`
- Pliki aplikacji (uploady itp.): `%APPDATA%\\HouseholdBudgetMate\\files`

## Szybki flow (Release)

1. Zbuduj instalator przez `dotnet build` (komenda wyżej) **lub** skrypt `build-msi.ps1`.
2. Zainstaluj wygenerowane `.msi`.
3. Uruchom aplikację ze skrótu w Start Menu.
4. Przy pierwszym uruchomieniu uzupełnij konfigurację, jeśli panel setup się wyświetli.
