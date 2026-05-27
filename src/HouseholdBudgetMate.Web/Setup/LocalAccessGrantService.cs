using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

namespace HouseholdBudgetMate.Web.Setup;

public interface ILocalAccessGrantService
{
    string? IssueGrantForRequest(HttpContext context, string purpose);
    bool IsValid(string? grant, string purpose);
    bool TryConsume(string? grant, string purpose);
}

public static class LocalAccessPurposes
{
    public const string AccessHardening = "access-hardening";
    public const string AccessRecovery = "access-recovery";
}

public sealed class LocalAccessGrantService : ILocalAccessGrantService
{
    public const string QueryParameterName = "grant";
    public const string DirectRemoteAddressItemKey = "HouseholdBudgetMate.DirectRemoteAddress";

    private static readonly TimeSpan GrantLifetime = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, GrantRecord> _grants = new(StringComparer.Ordinal);

    public string? IssueGrantForRequest(HttpContext context, string purpose)
    {
        if (!IsLoopbackRequest(context))
        {
            return null;
        }

        RemoveExpiredGrants();
        var grant = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _grants[grant] = new GrantRecord(purpose, DateTimeOffset.UtcNow.Add(GrantLifetime));
        return grant;
    }

    public bool IsValid(string? grant, string purpose)
    {
        if (string.IsNullOrWhiteSpace(grant)
            || !_grants.TryGetValue(grant, out var record))
        {
            return false;
        }

        if (record.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _grants.TryRemove(grant, out _);
            return false;
        }

        return string.Equals(record.Purpose, purpose, StringComparison.Ordinal);
    }

    public bool TryConsume(string? grant, string purpose)
    {
        if (!IsValid(grant, purpose))
        {
            return false;
        }

        return _grants.TryRemove(grant!, out _);
    }

    public static bool IsLoopbackRequest(HttpContext context)
    {
        var remoteAddress = context.Items.TryGetValue(DirectRemoteAddressItemKey, out var directAddress)
            ? directAddress as IPAddress
            : context.Connection.RemoteIpAddress;
        return remoteAddress is not null && IPAddress.IsLoopback(remoteAddress);
    }

    public static void CaptureDirectRemoteAddress(HttpContext context)
    {
        context.Items[DirectRemoteAddressItemKey] = context.Connection.RemoteIpAddress;
    }

    private void RemoveExpiredGrants()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var grant in _grants.Where(x => x.Value.ExpiresAtUtc <= now))
        {
            _grants.TryRemove(grant.Key, out _);
        }
    }

    private sealed record GrantRecord(string Purpose, DateTimeOffset ExpiresAtUtc);
}
