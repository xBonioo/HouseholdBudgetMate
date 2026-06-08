using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

namespace HouseholdBudgetMate.Web.Services;

/// <summary>
/// Thread-safe cache for the archive months list.
/// Data is scoped by budget owner, valid for the current calendar day (UTC), and refreshed on the next day or when
/// <see cref="Invalidate"/> is called (e.g. after closing or opening a new month plan).
/// </summary>
public sealed class ArchiveMonthsCacheService
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private volatile IReadOnlyList<AvailableMonthDto>? _cachedMonths;
    private string? _cachedBudgetOwnerUserId;
    private DateOnly _cacheDate = DateOnly.MinValue;

    public event Action? CacheChanged;

    /// <summary>
    /// Returns the semaphore used to serialise concurrent DB loads.
    /// Call <c>WaitAsync</c> before loading from the DB to avoid a cache stampede.
    /// </summary>
    public SemaphoreSlim Semaphore => _semaphore;

    /// <summary>
    /// Tries to return cached data that is still valid for today (UTC).
    /// Returns <see langword="false"/> when the cache is empty or stale.
    /// </summary>
    public bool TryGetCache(string? budgetOwnerUserId, out IReadOnlyList<AvailableMonthDto> months)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var snapshot = _cachedMonths;

        if (!string.IsNullOrWhiteSpace(budgetOwnerUserId)
            && snapshot is not null
            && _cacheDate == today
            && string.Equals(_cachedBudgetOwnerUserId, budgetOwnerUserId, StringComparison.Ordinal))
        {
            months = snapshot;
            return true;
        }

        months = [];
        return false;
    }

    /// <summary>Stores fresh data; sets the cache expiry to the end of today (UTC).</summary>
    public void UpdateCache(string? budgetOwnerUserId, IReadOnlyList<AvailableMonthDto> months)
    {
        if (string.IsNullOrWhiteSpace(budgetOwnerUserId))
        {
            Invalidate();
            return;
        }

        _cachedBudgetOwnerUserId = budgetOwnerUserId;
        _cachedMonths = months;
        _cacheDate = DateOnly.FromDateTime(DateTime.UtcNow);
        CacheChanged?.Invoke();
    }

    /// <summary>
    /// Clears the cache so the next request will reload from the database.
    /// Call this whenever month plans are added, closed or otherwise changed.
    /// </summary>
    public void Invalidate(string? budgetOwnerUserId = null)
    {
        if (!string.IsNullOrWhiteSpace(budgetOwnerUserId)
            && !string.Equals(_cachedBudgetOwnerUserId, budgetOwnerUserId, StringComparison.Ordinal))
        {
            return;
        }

        _cachedMonths = null;
        _cachedBudgetOwnerUserId = null;
        _cacheDate = DateOnly.MinValue;
        CacheChanged?.Invoke();
    }
}
