using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;

namespace HouseholdBudgetMate.Web.Services;

/// <summary>
/// Singleton, thread-safe cache for the archive months list.
/// Data is valid for the current calendar day (UTC) and refreshed on the next day or when
/// <see cref="Invalidate"/> is called (e.g. after closing or opening a new month plan).
/// </summary>
public sealed class ArchiveMonthsCacheService
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private volatile IReadOnlyList<AvailableMonthDto>? _cachedMonths;
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
    public bool TryGetCache(out IReadOnlyList<AvailableMonthDto> months)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var snapshot = _cachedMonths;

        if (snapshot is not null && _cacheDate == today)
        {
            months = snapshot;
            return true;
        }

        months = [];
        return false;
    }

    /// <summary>Stores fresh data; sets the cache expiry to the end of today (UTC).</summary>
    public void UpdateCache(IReadOnlyList<AvailableMonthDto> months)
    {
        _cachedMonths = months;
        _cacheDate = DateOnly.FromDateTime(DateTime.UtcNow);
        CacheChanged?.Invoke();
    }

    /// <summary>
    /// Clears the cache so the next request will reload from the database.
    /// Call this whenever month plans are added, closed or otherwise changed.
    /// </summary>
    public void Invalidate()
    {
        _cachedMonths = null;
        _cacheDate = DateOnly.MinValue;
        CacheChanged?.Invoke();
    }
}