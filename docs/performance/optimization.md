# Optimization Techniques in ASP.NET Applications

This document describes optimization guidelines aligned with the current architecture.

---

## 1. Data Access Optimization

### 1.1 `AsNoTracking` vs tracking

Use `AsNoTracking()` for read-only queries to reduce change-tracking overhead.

```csharp
var items = await context.Items
    .AsNoTracking()
    .ToListAsync(ct);
```

### 1.2 Projection to DTO

Prefer projection where full entity graphs are not needed.

```csharp
var items = await context.Items
    .AsNoTracking()
    .Select(x => x.ToDto())
    .ToListAsync(ct);
```

Benefits:
- less data materialization,
- lower memory usage,
- faster query execution in list endpoints.

### 1.3 Avoid oversized `Include` graphs

Only include relations required by a use case. For complex screens, evaluate split queries or targeted projections.

### 1.4 Split Queries

For queries with multiple collection `Include`s, use `AsSplitQuery()` to avoid cartesian explosion:

```csharp
var items = await context.Items
    .AsNoTracking()
    .Include(x => x.Tags)
    .Include(x => x.Comments)
    .AsSplitQuery()
    .ToListAsync(ct);
```

> Note: `MultipleCollectionIncludeWarning` is suppressed at startup — but splitting queries is still the recommended fix, not silencing warnings permanently.

### 1.5 Compiled Queries

For frequently executed, parameterized queries use `EF.CompileAsyncQuery` to avoid repeated query compilation overhead:

```csharp
private static readonly Func<ApplicationDbContext, int, Task<ItemDto?>> GetItemById =
    EF.CompileAsyncQuery((ApplicationDbContext ctx, int id) =>
        ctx.Items
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.ToDto())
            .FirstOrDefault());
```

Best suited for hot read paths called many times per second.

### 1.6 Pagination

Never load unbounded result sets. Always paginate list queries:

```csharp
var page = await context.Items
    .AsNoTracking()
    .OrderBy(x => x.CreatedAtUtc)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .Select(x => new ItemDto { ... })
    .ToListAsync(ct);
```

Use `FilterSearchModel` (from Abstractions) to carry page/size parameters consistently across services.

### 1.7 Batch Writes

For bulk inserts or updates, prefer `ExecuteUpdateAsync` / `ExecuteDeleteAsync` (EF Core 7+) over loading entities:

```csharp
await context.Items
    .Where(x => x.IsArchived)
    .ExecuteDeleteAsync(ct);

await context.Items
    .Where(x => x.CategoryId == oldCategoryId)
    .ExecuteUpdateAsync(s => s.SetProperty(x => x.CategoryId, newCategoryId), ct);
```

> Skips change tracking and `SaveChanges` overhead entirely — use only when business rules do not require entity-level processing.

### 1.8 Avoid `Count()` Before Any Query

Do not call `Count()` + `ToListAsync()` separately for the same filter. Use a single projection with `GroupBy` or a combined DTO, or accept the two-query cost only when needed for pagination metadata.

### 1.9 `AnyAsync` Over `CountAsync`

Prefer `AnyAsync` for existence checks — it generates a more efficient SQL `EXISTS`:

```csharp
// Bad
var count = await context.Items.CountAsync(x => x.Name == name, ct);
if (count > 0) ...

// Good
var exists = await context.Items.AnyAsync(x => x.Name == name, ct);
```

### 1.10 Soft Delete & `IgnoreQueryFilters`

Global query filters (`HasQueryFilter`) exclude soft-deleted rows automatically. For uniqueness checks across all rows use `IgnoreQueryFilters()` explicitly — do not remove the filter from the configuration.

---

## 2. DbContext Lifetime Strategy

### 2.1 Current project pattern

The project uses:

- `AddDbContextFactory<ApplicationDbContext>(...)` in startup,
- `IDbContextFactory<ApplicationDbContext>` injected into services,
- context creation per operation:

```csharp
await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
```

This pattern keeps context scope explicit and safe for concurrent request handling in Blazor Server.

### 2.2 About pooling

`AddDbContextPool` is **not** the currently used registration style.  
Do not switch registration mode without benchmark evidence and architecture decision.

### 2.3 Keep Context Lifetime Short

Open the context as late as possible, close it as soon as the operation finishes. Do not hold a context open across `await` boundaries that involve user interaction or I/O unrelated to the database.

---

## 3. Allocation and CPU Optimization

### 3.1 Loop Preferences

Prefer:
- `foreach` over heavy LINQ chains in hot paths,
- pre-sized collections when expected size is known (`new List<T>(capacity)`),
- avoiding unnecessary temporary lists.

Avoid:
- repeated `.ToList()` in loops,
- repeated enumeration of the same `IQueryable`.

### 3.2 `ValueTask` vs `Task`

Use `ValueTask<T>` for methods that very frequently complete synchronously (e.g., cached lookups, guard checks):

```csharp
public ValueTask<ItemDto?> GetFromCacheAsync(int id)
{
    if (_cache.TryGetValue(id, out var item))
        return ValueTask.FromResult<ItemDto?>(item);

    return new ValueTask<ItemDto?>(LoadFromDbAsync(id));
}
```

Do not use `ValueTask` blindly — it adds complexity for methods that are always async.

### 3.3 `Span<T>` and `Memory<T>`

For string parsing, slicing, or binary processing avoid allocating substrings — use `Span<char>` / `ReadOnlySpan<char>`:

```csharp
ReadOnlySpan<char> slice = input.AsSpan(startIndex, length);
```

### 3.4 `StringBuilder` for String Concatenation

Avoid `+` or string interpolation inside loops. Use `StringBuilder` when building output iteratively.

### 3.5 `string.IsNullOrWhiteSpace` Over `== null || == ""`

Use the built-in guard methods consistently — they are readable and slightly cheaper than manual checks.

### 3.6 Avoid Boxing

Prefer generic collections (`List<int>`) over `ArrayList`. Avoid passing value types as `object` in hot paths.

---

## 4. API and Middleware Performance

- Keep middleware short-circuit logic cheap.
- For API key checks, avoid expensive parsing before quick guards.
- Keep serialization payloads minimal for error responses.
- Register `ExceptionHandlingMiddleware` early to prevent redundant pipeline execution on errors.
- Avoid reading `HttpRequest.Body` multiple times — enable buffering explicitly if needed.

---

## 5. Async and I/O Guidelines

- Use async EF methods (`ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`).
- Avoid blocking (`.Result`, `.Wait()`).
- Use `Task.WhenAll` only for independent I/O operations.
- Pass `CancellationToken` through every async call chain — it enables early cancellation and prevents orphaned DB queries.
- Use `ConfigureAwait(false)` in library-level code where no `SynchronizationContext` is needed (Application layer is a good candidate).

---

## 6. Caching

### 6.1 `IMemoryCache`

`IMemoryCache` is registered (`AddMemoryCache`) and available for in-process caching.

Pattern for absolute-expiry cache:

```csharp
if (!_cache.TryGetValue(cacheKey, out ItemDto? cached))
{
    cached = await LoadFromDbAsync(id, ct);
    _cache.Set(cacheKey, cached, TimeSpan.FromMinutes(5));
}
return cached;
```

### 6.2 Cache Key Design

Use structured, deterministic keys:

```csharp
var key = $"item:{id}";
var key = $"items:category:{categoryId}:page:{page}";
```

### 6.3 Cache Invalidation

Invalidate on write operations in the same service method:

```csharp
await context.SaveChangesAsync(ct);
_cache.Remove($"item:{request.Id}");
```

### 6.4 When NOT to Cache

- Data that changes frequently and must be consistent (financial state, write-heavy entities).
- Large objects that can cause memory pressure without size limits.
- Per-user data mixed with shared keys (risk of data leakage).

Use `MemoryCacheEntryOptions` with `SizeLimit` when the cache holds many items.

---

## 7. Logging Performance

### 7.1 Structured Logging with Serilog

This project uses Serilog. Always use structured parameters, not string interpolation:

```csharp
// Bad — allocates a string even if log level is filtered out
logger.LogInformation($"Processing item {id}");

// Good — deferred rendering, structured
logger.LogInformation("Processing item {ItemId}", id);
```

### 7.2 Log Level Guards

For expensive log-line construction, guard with level checks:

```csharp
if (logger.IsEnabled(LogLevel.Debug))
    logger.LogDebug("Snapshot: {Data}", JsonSerializer.Serialize(data));
```

### 7.3 `LoggerMessage` Source Generator

For the highest-frequency log calls, use the `[LoggerMessage]` source generator to avoid boxing and closure allocations:

```csharp
public static partial class AppLogs
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Item {ItemId} created")]
    public static partial void ItemCreated(ILogger logger, int itemId);
}
```

### 7.4 Request Logging Threshold

`UseSerilogRequestLoggingWithThreshold()` is applied — ensure the threshold filters out health checks and static asset requests to avoid log noise.

---

## 8. Blazor Server Performance

### 8.1 Minimize Re-renders

Override `ShouldRender()` in components that receive frequent parameter updates but do not always need to re-render:

```csharp
protected override bool ShouldRender() => _isDirty;
```

### 8.2 Virtualization for Large Lists

Use `<Virtualize>` for lists that can grow beyond ~50 items:

```razor
<Virtualize Items="@items" Context="item">
    <ItemRow Item="@item" />
</Virtualize>
```

This avoids rendering all DOM nodes upfront.

### 8.3 Avoid Unnecessary `StateHasChanged`

Call `StateHasChanged()` only when the component state has actually changed. Avoid calling it inside loops or from background tasks that fire more frequently than needed.

### 8.4 Background Tasks and UI Thread

When updating UI from background tasks, marshal back to the render thread:

```csharp
await InvokeAsync(StateHasChanged);
```

### 8.5 Lazy Loading of Heavy Components

Defer loading of heavy modals/dialogs with `@if` guards — do not render them until the user triggers the action.

### 8.6 Circuit Scope vs Singleton

Services registered as `Scoped` live for the lifetime of the Blazor circuit. Avoid holding large state in scoped services — keep them focused on use-case execution.

---

## 9. PostgreSQL-Specific Guidelines

### 9.1 Index Coverage

Ensure all columns used in frequent `WHERE`, `ORDER BY`, and `JOIN` clauses have appropriate indexes. Define indexes in `IEntityTypeConfiguration<T>`:

```csharp
builder.HasIndex(x => x.CreatedAtUtc);
builder.HasIndex(x => new { x.CategoryId, x.IsDeleted });
```

### 9.2 Partial Indexes

For soft-deleted tables, a partial index on active rows can dramatically reduce index size and lookup cost:

```csharp
builder.HasIndex(x => x.Name)
    .HasFilter("\"IsDeleted\" = false");
```

### 9.3 `LIKE` vs `ILIKE` vs Full-Text Search

- `LIKE '%text%'` cannot use a standard B-tree index — avoid for large tables.
- Use `EF.Functions.ILike` (Npgsql) for case-insensitive matching with `pg_trgm` trigram index support.
- For full-text search scenarios, use `tsvector` / `tsquery` via Npgsql EF extensions.

### 9.4 JSONB Columns

For semi-structured data, prefer `jsonb` over `text` — enables indexed queries and PostgreSQL JSON operators.

### 9.5 Connection Resilience

The project configures `EnableRetryOnFailure(5, 30s)` — do not lower these values for production. Transient network errors in containerized environments are common.

### 9.6 `CommandTimeout`

`CommandTimeout(60)` is set globally. For individual long-running operations (e.g., bulk import), override per-context:

```csharp
context.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
```

---

## 10. Benchmarking Strategy

Every non-trivial optimization should compare at least two approaches and measure:

- execution time,
- memory allocation,
- behavior under realistic data volume.

Suggested scenarios in this codebase:
- `Include`-heavy query vs projection,
- `AsTracking` vs `AsNoTracking`,
- compiled query vs non-compiled on hot endpoints,
- `ExecuteDeleteAsync` vs load-and-remove for bulk deletes,
- `IMemoryCache` hit vs DB round-trip latency.

Use **BenchmarkDotNet** for micro-benchmarks and EF Core logging (`LogTo`) to inspect generated SQL during development.

---

## 11. Performance Anti-Patterns

Avoid:
- N+1 queries (use `Include` or projection — but not both unnecessarily),
- returning full entities when DTO projection is enough,
- broad filtering in memory when SQL can do it,
- unbounded cache entries,
- holding `DbContext` open across user interaction,
- calling `Count()` + `ToList()` on the same query separately when only one is needed,
- `string.Format` or `$""` interpolation inside logger calls,
- `async void` methods (use `async Task` everywhere),
- `Task.Run` wrapping sync code to fake async — it wastes thread-pool threads.

---

## 12. Practical Rule for This Repository

Optimize where evidence shows bottlenecks. Keep code readable first, then tune measured hot paths while preserving architectural constraints (`IDbContextFactory`, layered boundaries, DTO contracts）。

Quick decision heuristic:
1. Is the bottleneck in the database? → check query plan, add index, use projection.
2. Is the bottleneck in serialization / rendering? → reduce payload, use virtualization.
3. Is the bottleneck in allocation? → profile with dotMemory / dotTrace, then use `Span<T>`, pooling, or `ValueTask`.
4. Is the bottleneck in repeated work? → add `IMemoryCache` with a short TTL.

---

## 13. Performance Optimization Checklist

- [ ] Read-only queries use `AsNoTracking()`
- [ ] List endpoints project to DTO with `Select()`
- [ ] Pagination applied to all unbounded list queries
- [ ] Multi-collection includes evaluated for `AsSplitQuery()`
- [ ] Hot read paths evaluated for compiled queries
- [ ] Bulk mutations evaluated for `ExecuteUpdateAsync` / `ExecuteDeleteAsync`
- [ ] Indexes defined for all `WHERE` / `ORDER BY` columns
- [ ] Partial indexes added for soft-delete tables
- [ ] `IMemoryCache` used for stable, frequently-read reference data
- [ ] Cache invalidated on relevant write operations
- [ ] All log calls use structured parameters (no interpolation)
- [ ] `CancellationToken` passed through all async call chains
- [ ] Blazor lists with >50 items use `<Virtualize>`
- [ ] No `async void` methods outside Blazor event handlers
