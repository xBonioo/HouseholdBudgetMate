# Reference Architecture Guide

## Purpose
This document captures the architecture principles extracted from the reference project and rewrites them as a simple, reusable layered approach.

This guide intentionally excludes:
- MediatR
- CQRS
- pipeline behaviors
- over-abstracted handler chains

---

## 1. Layers and Responsibilities

### 1.1 Presentation (UI / API)
Scope:
- pages, forms, components, API endpoints
- input validation at UI/form level
- mapping input data to request contracts
- calling application services directly

Must not do:
- business workflow logic
- direct database queries
- domain entity manipulation

### 1.2 Application (Use Case Services)
Scope:
- use case implementation
- business rules and workflow transitions
- orchestration of reads, writes, history, side effects
- mapping between Request, Entity, and Dto/Result contracts
- FluentValidation validators (per-feature, per-operation)
- custom exception types (`Kernel/Exceptions`)
- cross-cutting helpers: `IDateTimeProvider`, `ApplicationConfiguration`

Must not do:
- UI rendering
- HTTP protocol concerns

### 1.3 Abstractions
Scope:
- service interfaces (`IXxxService`)
- contract types (`*Request`, `*Dto`, optional `*Result`)
- shared enums, models (`FilterSearchModel`, `IServiceResult`, etc.)
- extension helpers on primitives and enums

Rules:
- **zero external dependencies** — only `System.*` namespaces allowed
- all interfaces and classes must be `public`
- DTOs must be `public sealed`
- Requests must be `public` (not sealed — allow inheritance if needed)
- contracts are organized by feature: `Contracts/<Feature>/{Requests|Dto|Results}`

### 1.4 Domain
Scope:
- entities and relationships
- domain enums and core models
- ORM entity configuration (`IEntityTypeConfiguration<T>`)
- domain infrastructure: `ATimestampable`, `IEntityId`, `ITimestampable`

Characteristics:
- persistence-centric entities
- workflow logic lives in Application services
- entities are not returned to the UI

### 1.5 Migrations
Scope:
- `ApplicationDbContext` and migrations
- database provider configuration
- `SaveChanges` / `SaveChangesAsync` override for automatic timestamp handling

### 1.6 Tests
Scope:
- architecture boundary tests (using NetArchTest)
- service unit tests
- shared test helpers (`TestDbContextFactory`, `StaticDateTimeProvider`)

---

## 2. Dependency Direction

**Project reference chain (actual .csproj dependencies):**
```
Web -> Application -> Migrations -> Domain
Web -> Application -> Abstractions
```

**Call chain direction (runtime flow):**
```
Presentation (Web) -> Application services -> ApplicationDbContext (via IDbContextFactory)
```

Not allowed:
- Presentation -> DbContext directly
- Presentation -> Domain entities directly
- Application -> Presentation
- returning domain entities to UI/API
- Domain -> any other project
- Abstractions -> any project other than System

---

## 2.1 DbContext Access Pattern

This project uses `IDbContextFactory<ApplicationDbContext>` registered as `AddDbContextFactory<ApplicationDbContext>` in `Program.cs`.

Application services must create a context per operation:

```csharp
public class ItemService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
{
    public async Task<ItemDto> GetByIdAsync(int id, CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        // ...
    }
}
```

Rules:
- **Never** inject `ApplicationDbContext` directly as scoped — always use `IDbContextFactory`.
- **Never** call the factory from Blazor components — always go through an Application service.
- Use `AsNoTracking()` for read-only queries.
- Use `SaveChangesAsync(ct)` for all write operations.

---

## 2.2 Automatic Timestamp Handling

`ApplicationDbContext` overrides `SaveChanges` and `SaveChangesAsync` to automatically maintain `CreatedAtUtc` and `UpdatedAtUtc` on all entities implementing `ITimestampable`:

```csharp
public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    UpdateTimestamps();
    return base.SaveChangesAsync(cancellationToken);
}
```

All entity configurations should apply these columns as required.

---

## 3. End-to-End Data Flow

Standard flow:
1. User action arrives in a Blazor component or API endpoint.
2. Presentation builds a request DTO from input.
3. Presentation calls an Application service method directly.
4. Service validates the request via FluentValidation — throws `BadRequestException` on failure.
5. Service loads data through an operation-scoped `DbContext`.
6. Service applies business rules and updates state.
7. Service saves changes via `SaveChangesAsync`.
8. Service maps entities to response DTOs using explicit extension mapping methods.
9. Presentation receives DTOs and renders results.

Read flow rule:
- use no-tracking queries for read-only paths.

---

## 4. DTO and Data Contract Rules

### 4.1 Contract Types
- `*Request`
  - input payload for state-changing use cases
- `*Dto`
  - output payload for UI/API consumption
- `*Result` (optional)
  - operation result payload (for create/action operations)
- `*Response` (optional)
  - envelope contract for API responses

### 4.2 DTO Rules
- `*Dto` classes must be `public sealed`.
- `*Request` classes must be `public` (not sealed).
- DTOs must not contain ORM entities or lazy-loaded references.
- External DTOs may flatten relational data for display.
- Write operations may return a `Dto` or `Result` — never raw entities.
- API endpoints may wrap payloads in `Response` for consistent status/message handling.

### 4.3 Naming and Folder Conventions
- Names: `CreateXRequest`, `XDto`, optional `XResult`
- Folder layout under `Abstractions`:
  ```
  Contracts/
    <Feature>/
      Requests/
        CreateXRequest.cs
        UpdateXRequest.cs
        DeleteXRequest.cs
      Dto/
        XDto.cs
  ```
- All types in `Abstractions` must be `public`.

### 4.4 Data Boundaries
- External boundary (UI/API): contracts from Abstractions only.
- Internal boundary (Application/Domain): entities and domain models.
- Mapping happens at layer boundaries, never by leaking entities outward.

---

## 5. Service and Use Case Pattern

### 5.1 Minimal Flow Without Mediator Patterns
Replace:
- Controller -> Handler -> Mediator -> Service

With:
- Component/Controller -> Application Service -> DbContext

### 5.2 Service Method Shape
Each use case is a direct service method:
- `GetAllAsync(CancellationToken ct)`
- `GetByIdAsync(id, CancellationToken ct)`
- `CreateXAsync(CreateXRequest request, CancellationToken ct)`
- `UpdateXAsync(UpdateXRequest request, CancellationToken ct)`
- `DeleteXAsync(DeleteXRequest request, CancellationToken ct)`

Rules:
- always asynchronous
- `CancellationToken` on every method
- explicit validation before any database access
- explicit business exceptions (see Section 6)
- no intermediate handler layer

### 5.3 Validators in Services
Validators (FluentValidation) are instantiated as `private static readonly` fields directly in the service class — one per operation:

```csharp
public sealed class ItemService(IDbContextFactory<ApplicationDbContext> dbContextFactory, ...)
{
    private static readonly CreateItemRequestValidator CreateValidator = new();
    private static readonly UpdateItemRequestValidator UpdateValidator = new();
    private static readonly DeleteItemRequestValidator DeleteValidator = new();

    public async Task<ItemDto> CreateItemAsync(CreateItemRequest request, CancellationToken ct)
    {
        CreateValidator.ValidateOrThrowBadRequest(request);
        // ...
    }
}
```

`ValidateOrThrowBadRequest<T>` is an extension method on `IValidator<T>` that throws `BadRequestException` with concatenated error messages on validation failure.

### 5.4 Larger Module Organization
- split large services into partial files by functional area
- keep one coherent service interface per module

---

## 6. Exception Handling

### 6.1 Custom Exception Types
All business exceptions reside in `Application.Kernel.Exceptions`:

| Exception | HTTP status |
|---|---|
| `BadRequestException` | 400 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `ImportException` | 422 |
| `InternalException` | 500 |
| `UnavailableException` | 503 |
| `UnimplementedException` | 501 |

### 6.2 ExceptionHandlingMiddleware
A global `ExceptionHandlingMiddleware` in the Web project maps domain exceptions to HTTP status codes. On GET requests, unhandled exceptions redirect to an `/Error` page. All other requests receive a plain-text error response.

Register it early in the pipeline, before `UseExceptionHandler`:

```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();
```

---

## 7. Domain Entity Conventions

### 7.1 Base Abstractions

All domain entities (except log/audit entities) must:
- inherit `ATimestampable` — provides `CreatedAtUtc` and `UpdatedAtUtc` (auto-maintained by DbContext)
- implement `IEntityId` — provides `int Id { get; set; }`

```csharp
public sealed class Item : ATimestampable, IEntityId
{
    public int Id { get; set; }
    // ...
}
```

### 7.2 Soft Delete Pattern
Entities that support soft delete expose:
```csharp
public bool IsDeleted { get; set; } = false;
public DateTime? DeletedAtUtc { get; set; }
```

Entity configurations apply a global query filter:
```csharp
builder.HasQueryFilter(x => !x.IsDeleted);
```

When a service needs to query across all records (e.g., for uniqueness checks), it uses `IgnoreQueryFilters()`:
```csharp
await dbContext.Items
    .IgnoreQueryFilters()
    .AnyAsync(x => !x.IsDeleted && x.Name == name, ct);
```

Soft delete uses `IDateTimeProvider` to record the deletion time:
```csharp
entity.IsDeleted = true;
entity.DeletedAtUtc = dateTimeProvider.GetUtcDateTime();
await dbContext.SaveChangesAsync(ct);
```

### 7.3 Entity Configuration
Every entity has a dedicated `IEntityTypeConfiguration<T>` class in the Domain project, under `EntityConfiguration/`. Configurations are registered automatically:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(DomainAssemblyMarker).Assembly);
```

---

## 8. Mapping Strategy

### 8.1 Technology-Agnostic Rules
Mapping points:
- `Request -> Entity` for writes in Application
- `Entity -> Dto` for reads in Application
- return `Dto` or `Result` to Presentation

### 8.2 Manual Mapping (Default)
Preferred default: explicit static extension methods in a dedicated `Mapping/` folder in the Application project.

```csharp
// Application/Mapping/ItemExtensionMapping.cs
public static class ItemExtensionMapping
{
    public static ItemDto MapItem(this Item item) => new()
    {
        Id = item.Id,
        Name = item.Name
        // ...
    };
}
```

Naming convention: `<Feature>ExtensionMapping.cs`

### 8.3 Query-Level Projection
For list queries, prefer `Select(x => new XDto { ... })` directly in LINQ instead of loading entities and then mapping — reduces memory pressure.

---

## 9. IDateTimeProvider

All date/time operations within Application services must use `IDateTimeProvider` (injected via constructor) instead of `DateTime.UtcNow` directly.

Available methods:
- `GetUtcDateTime()` / `GetUtcDateTimeOffset()` / `GetUtcDateOnly()`
- `GetLocalDateTime()` / `GetLocalDateTimeOffset()` / `GetLocalDateOnly()`
- `GetLocalTimeZoneInfo()` / `GetTimeZoneInfo(string id)`

Registered as singleton in DI:
```csharp
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
```

Tests use `StaticDateTimeProvider` with a fixed value.

---

## 10. ApplicationConfiguration

A single sealed configuration class loaded from `appsettings.json` under the `"Application"` section and registered as singleton:

```csharp
var config = builder.Configuration
    .GetSection("Application")
    .Get<ApplicationConfiguration>()
    ?? throw new InvalidOperationException("Application configuration is missing");
builder.Services.AddSingleton(config);
```

Key properties:
- `Name`, `Title`, `ServiceName` — used for logging and telemetry
- `Timezone` — used by `DateTimeProvider`
- `MigrateDatabaseOnStart` — auto-run migrations on startup
- `SeedDataToDatabase` — trigger data seeding on startup
- `LogCleanupTask` — enable periodic log cleanup

`ApplicationConfiguration` must be `public sealed` with no inheritance.

---

## 11. Startup Sequence

The recommended `Program.cs` startup sequence:

1. Register `AddDbContextFactory<ApplicationDbContext>` with retry policy
2. Register Blazor / Razor components
3. Register infrastructure singletons (`IDateTimeProvider`, `IStoragePathProvider`)
4. Register application services as `Scoped`
5. Register seed services as `Scoped`
6. Load and register `ApplicationConfiguration` as singleton
7. Configure Serilog logging
8. Build app
9. Register middleware pipeline: Serilog -> `ExceptionHandlingMiddleware` -> HTTPS -> localization
10. Run migrations if `MigrateDatabaseOnStart` is set
11. Run seed services
12. Map routes and run

---

## 12. Seeding Pattern

Seed services (`XxxSeedService`) are registered as `Scoped` and invoked during startup using a dedicated scope:

```csharp
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<XxxSeedService>();
    await seeder.SeedAsync(CancellationToken.None);
}
```

Seed services use `IDbContextFactory` directly (same as Application services) and are idempotent — they check for existing records before inserting.

---

## 13. Architecture Tests

Architecture rules are enforced by automated tests using **NetArchTest** in the `.Tests` project under `Tests/Architecture/`.

### Enforced rules (examples)

**Abstractions layer:**
- All interfaces and classes are `public`
- `*Dto` types are `public sealed` and reside in a namespace ending with `Dto`
- `*Request` types are `public` (not sealed) and reside in a namespace ending with `Requests`
- Abstractions project has no external dependencies beyond `System.*`

**Application layer:**
- Application assembly must not depend on `*.Web`
- Services must not depend on presentation layers
- Extensions classes in `Extensions` namespace must be `static` and end with `Extensions`
- Configuration classes must be `sealed`, `public`, and have no base class

**Domain layer:**
- All entities (except log entities) must inherit `ATimestampable`
- Domain assembly must not depend on Application or Web

### Test helpers
- `StaticDateTimeProvider` — fixed `IDateTimeProvider` for deterministic tests
- `TestDbContextFactory` — in-memory or test database factory
- `ThrowingApplicationDbContext` — DbContext that throws on unexpected calls

---

## 14. Simplification Rules

DO:
- call application services directly from UI/API
- keep controllers/components thin
- keep business orchestration in Application services
- keep mapping explicit at boundaries
- use FluentValidation for all input validation in services

DO NOT:
- introduce MediatR
- split use cases into CQRS command/query handlers
- rely on pipeline behaviors for core flow
- build deep handler orchestration chains
- inject `ApplicationDbContext` directly (always use `IDbContextFactory`)
- use `DateTime.UtcNow` directly in services (use `IDateTimeProvider`)
- return domain entities to the UI

---

## 15. New Module Checklist
- [ ] define entities (inherit `ATimestampable`, implement `IEntityId`, add soft delete if needed)
- [ ] create `IEntityTypeConfiguration<T>` in Domain
- [ ] create `*Request` and `*Dto` contracts in Abstractions under `Contracts/<Feature>/`
- [ ] add service interface in `Abstractions/Interfaces/`
- [ ] implement service in Application with `IDbContextFactory`, `IDateTimeProvider`
- [ ] add static readonly FluentValidation validators in the service
- [ ] add explicit mapping extension methods in `Application/Mapping/`
- [ ] register service as `Scoped` in `Program.cs`
- [ ] add architecture boundary tests (ArchTest)
- [ ] add service unit tests

---

## 16. Summary
The target architecture is intentionally simple: clear layers, explicit contracts, direct use case services, and no unnecessary architectural indirection.

Primary rule: preserve responsibilities and boundaries, remove unnecessary complexity.
