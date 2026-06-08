# Household Budget Mate — Project Specification

## Overview

A self-hosted household budget management application running as a standalone `.exe`.
Accessible via browser on LAN (desktop and mobile). Built for long-term extensibility.

- **Runtime:** .NET 10
- **Access:** `http://192.168.x.x:7135` (LAN only)
- **Target:** Single household, single user (multi-user planned — see Pre-Sprint 11)
- **Currency:** PLN only (multi-currency planned later)

---

## Tech Stack

### Backend / Frontend
- ASP.NET Core (.NET 10)
- Blazor Server (SSR + interactivity)

### Database
- PostgreSQL
- Entity Framework Core (code-first, migrations)
- All monetary values use `decimal` — never `double` or `float`

### UI
- MudBlazor components
- Tailwind CSS (if needed beyond MudBlazor)
- Chart.js via JSInterop

### Background Jobs
- Hangfire or Quartz.NET for:
  - OCR processing
  - Recurring expense generation
  - Statistics computation
  - Scheduled backups

### Events (lightweight event-driven)
- `ExpenseCreatedEvent`
- `ExpenseUpdatedEvent`
- `ReceiptScannedEvent`
- `BudgetExceededEvent`

---

## First-Run Setup

On first launch, the app detects a missing `config.json` and redirects to `/setup`.

The user provides: host, port, login, password, database name.

Config is saved to `config.json` next to the `.exe`. On subsequent launches, the app reads `config.json`, runs any pending migrations, and starts normally.

> Setup flow is implemented in Sprint 16. Until then, `config.json` is created manually.

---

## Solution Structure

```
HouseholdBudgetMate.slnx
└── src/
    ├── HouseholdBudgetMate.Domain/       # Entities, value objects, domain rules, events, base classes
    ├── HouseholdBudgetMate.Application/  # Use cases, commands, queries, handlers, DTOs, mapping
    ├── HouseholdBudgetMate.Abstractions/ # Interfaces, shared contracts (ICurrentUserService etc.)
    ├── HouseholdBudgetMate.Migrations/   # EF Core DbContext (ApplicationDbContext), migrations
    ├── HouseholdBudgetMate.Web/          # Blazor Server pages, components, JSInterop
    └── HouseholdBudgetMate.Tests/        # Unit + integration tests
```

**Dependency rules (enforced):**
- `Domain` → no dependencies
- `Migrations` → `Domain`
- `Application` → `Abstractions`, `Migrations` (uses `IDbContextFactory<ApplicationDbContext>`)
- `Web` → `Application` only — never calls EF Core directly
- `Tests` → `Application`, `Domain`

---

## Entity Base Classes

All entities live in `Domain/` and follow these conventions:

**`IEntityId`** — requires `int Id { get; set; }`. Every entity implements this.

**`ATimestampable`** (implements `ITimestampable`) — provides `CreatedAtUtc DateTime` and `UpdatedAtUtc DateTime`, auto-set by `ApplicationDbContext.SaveChanges`. All entities inherit this.

**Soft-deletable entities** additionally define `bool IsDeleted` and `DateTime? DeletedAt`. Applies to: `Category`, `Tag`, `Account`.

Global EF Core query filter excludes soft-deleted records by default on all soft-deletable entities.

```csharp
public class Category : ATimestampable, IEntityId
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    // ...
}
```

---

## Domain Rules (Business Logic)

### Monetary values
Always `decimal`. Never `double` or `float`. This applies to every field representing money across all entities.

### Soft delete
`Category`, `Tag`, and `Account` use soft delete (`IsDeleted` + `DeletedAt`). Never hard-delete these. Soft-deleted entities remain visible in historical data but are excluded from pickers and new entries via global EF filter.

### Monthly financial indicators

The month view presents two separate financial indicators. They are not aliases:

- **Plan remaining (`Pozostalo`)** explains execution of planned expense lines only.
- **Live balance** answers how much liquid cash is available after recorded movements.

Monthly account entries are **closing balances**. For an open month, the live calculation requires the closing balance from the immediately preceding calendar month for each applicable non-savings account. A closed historical month is read-only and is calculated from the latest stored non-savings closing balances before that month, without retroactively declaring absent rows incomplete. Savings accounts are excluded from liquidity. An archived account is applicable only for selected months that ended before it was archived; an account archived during a month is not required to have that month's closing balance. If a legacy archived record lacks `ArchivedAtUtc`, `UpdatedAtUtc` is used as the best available archival timestamp.

```
LiveBalance = SUM(immediately preceding-month closing balances of applicable non-savings accounts)
            + SUM(incomes in the month with ExpectedDayOfMonth <= today)
            - SUM(actual expense amounts in the month)
            - SUM(savings transfers in the month with TransferDate <= today)
```

- `LiveBalance` is computed live and is never stored as a column.
- Income recognition remains date-based in the MVP: it contributes when its expected date is reached.
- Actual expenses reduce `LiveBalance`.
- A savings transfer reduces `LiveBalance` when due.
- For an open month, if any applicable non-savings account lacks a closing balance for the immediately preceding month, `LiveBalance` is incomplete and must not be displayed as a reliable amount.
- For a closed historical month, missing balance rows do not block display; the result uses only available prior persisted balances because the period is read-only.
- A stored closing-balance row with value `0` is complete input; an absent row is missing data and must be distinguished in the UI.

### Envelope budget

- A per-category monthly spending cap stored on `Category.EnvelopeLimit decimal?` (nullable — not every category has a cap).
- Exceeding the cap **does not block** saving an expense. UI signals the breach visually (progress bar turns red, warning shown).
- The cap does not carry over to the next month automatically.
- Effective from the moment it is set — does not apply retroactively to past months.
- `BudgetExceededEvent` is emitted when the category total crosses the limit (for future notifications).

### Line items

- Controlled per category by `Category.AllowsLineItems bool`.
- When a category allows line items, the expense's `ActualAmount` = `SUM(ExpenseLineItem.Amount)` automatically.
- When no line items exist, `ActualAmount` is entered manually.
- Line items have no `PlannedAmount` — only `ActualAmount`, `Description`, `Date` (DateOnly), and optional `TagId`.

### Recurring expenses

- Auto-generate as planned `Expense` entries when a new month is opened.
- Generation is **idempotent**: before inserting, check for an existing `Expense` with matching `RecurringExpenseId` + `MonthPlanId`. Skip if found.
- If a recurring expense's amount changes, future months use the new amount; past months are unaffected.
- Recurring incomes (`Income.IsRecurring = true`) follow the same generation pattern.

### Month lifecycle

- `MonthPlan.Status` is either `open` or `closed`.
- Closing a month: status set to `closed`, triggers recurring expense and income generation for the next month.
- Re-opening a closed month requires explicit user confirmation.
- Closed months are read-only — no edits to their expenses or incomes.

### Audit trail

Every create / update / delete on `Expense`, `Income`, `Account`, `Category` writes an `AuditLog` entry via EF Core `SaveChangesInterceptor`. Old and new values stored as JSON.

```json
{
  "operation": "ExpenseUpdated",
  "entityType": "Expense",
  "entityId": 42,
  "oldValues": { "actualAmount": 100.00 },
  "newValues": { "actualAmount": 150.00 },
  "changedAtUtc": "2024-03-15T14:22:00Z"
}
```

### Operational logs and retention

`AuditLogs` are the accepted administrator-visible financial change history and are not removed by operational log retention. The `Logs` table is for runtime diagnostics only and is cleaned according to `Application:LogRetentionDays` when `Application:LogCleanupTask` is enabled. New operational logs should avoid full financial payloads, receipt contents, database connection details, and other sensitive household data unless there is a specific diagnostic need. Production Blazor detailed errors stay disabled so exception details remain in server logs rather than public UI responses.

---

## Data Model

> All entities inherit `ATimestampable` (`CreatedAtUtc`, `UpdatedAtUtc`) and implement `IEntityId` (`int Id`).
> Only domain-specific fields are listed below.

### Category
```
Category {
    Id, Name, Color, Icon,
    AllowsLineItems bool,
    EnvelopeLimit decimal?,
    IsDeleted, DeletedAt?
}
```

### Tag
```
Tag { Id, Name, CategoryId, IsDeleted, DeletedAt? }
```
Tags are scoped to a category (e.g. "Spożywcze" → "Biedronka", "Lidl").

### MonthPlan
```
MonthPlan { Id, Month, Year, Status (open|closed) }
```

### Expense
```
Expense {
    Id, MonthPlanId, Name,
    CategoryId, TagId?,
    PlannedAmount decimal, ActualAmount decimal,
    IsRecurring bool, RecurringExpenseId?,
    ShowRemainingInUI bool, IsUnplanned bool,
    IsDeleted, DeletedAt?
}
```

### ExpenseLineItem
```
ExpenseLineItem { Id, ExpenseId, Description, Amount decimal, Date DateOnly, TagId? }
```

### Account
```
Account { Id, Name, Type (cash|bank|savings|other), IsArchived, ArchivedAtUtc? }
AccountMonthBalance { Id, AccountId, Year, Month, ClosingBalance decimal }
```
Monthly `ClosingBalance` entries provide the required base for an open immediately following month's `LiveBalance`. Closed historical months use the latest available persisted prior balances. `savings` accounts are excluded from the calculation; archived accounts are included only for selected months completed before they were archived.

### Income
```
Income {
    Id, Month, Year, Name,
    Amount decimal, ExpectedDayOfMonth DateOnly?,
    AccountId, IsRecurring bool
}
```
Covers both regular (salary, benefits) and irregular (OLX sale, one-off) income.

### RecurringExpense
```
RecurringExpense { Id, Name, CategoryId, Amount decimal, DayOfMonth int, IsActive bool }
```

### EnvelopeBudget
```
EnvelopeBudget { Id, CategoryId, Month, Year, LimitAmount decimal }
```
Stored separately from `Category.EnvelopeLimit` to preserve per-month history. `Category.EnvelopeLimit` is the current default; `EnvelopeBudget` records the actual limit applied in a given month.

### AuditLog
```
AuditLog { Id, EntityType, EntityId, Operation, OldValues JSON, NewValues JSON, ChangedAtUtc }
```

### OcrRawResult *(Sprint 17+)*
```
OcrRawResult { Id, UploadedAt, RawText, Status (pending|processing|done|failed), ExpenseId? }
```

### OcrMappingRule *(Sprint 18+)*
```
OcrMappingRule { Id, Keyword, CategoryId, TagId? }
```

---

## Security

### Phase 1 (current)
- No authentication — LAN access only, trusted network assumed.
- `NegotiateAuthentication` (Windows auth) must be removed from `Program.cs` — it is added by the default .NET template and must not be present in Phase 1.

### Phase 2 (Sprint 24)
- ASP.NET Identity, optionally enabled via `"AuthEnabled": true` in `config.json`.
- PIN or password login.
- `ICurrentUserService` swapped from hardcoded `"default-user"` to Identity-backed implementation.

---

## Multi-User Preparation (Pre-Sprint 11)

Before Phase 4 work begins, all entities receive `UserId string` and a global EF Core filter by `UserId`. `ICurrentUserService` in `Abstractions` returns `"default-user"` in Phase 1.

`HouseholdMode` stored in `config.json`:
- `SharedBudget` — spouse sees the same budget (shared `UserId`)
- `SeparateBudget` — each user has their own data (different `UserId`)

Swapping in real auth in Sprint 24 requires only replacing `ICurrentUserService` — no entity or query changes.

---

## Localization

- Polish (`pl`) and English (`en`) supported at launch via `.resx` resource files.
- Language switcher in settings, stored in `config.json` or cookie.
- Community contributors can add languages via GitHub PR — see `CONTRIBUTING.md`.

---

## Future Features (keep architecture unblocked)

| Feature | What to avoid now |
|---|---|
| External access (VPN) | Don't hardcode `http://` — keep scheme configurable |
| Multi-user / household | `ICurrentUserService` pattern already in place |
| Multi-currency | Don't hardcode PLN symbol in domain logic — only in UI formatting |
| Investment portfolios | Separate bounded context, no entanglement with `Expense` |
| Financial goals | Separate entity, linked to `Account` |
| Spending forecasts | Read-only from history — no write-side impact |
| Push alerts / notifications | Event infrastructure already planned |
| Backups | Scheduled Hangfire job — slot in `config.json` |
| REST API for Home Assistant | Thin controller layer in `Web/` — no domain changes needed |
| JDG / business cost tracking | Separate `BusinessExpense` bounded context |
