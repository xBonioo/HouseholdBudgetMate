using HouseholdBudgetMate.Application.Kernel.Timing;

namespace HouseholdBudgetMate.Tests.Shared;

public class StaticDateTimeProvider(DateTime now) : IDateTimeProvider
{
    private readonly TimeZoneInfo _tz = TimeZoneInfo.Utc;
    public DateTime GetLocalDateTime() => TimeZoneInfo.ConvertTimeFromUtc(now, _tz);
    public DateTimeOffset GetLocalDateTimeOffset() => new DateTimeOffset(GetLocalDateTime());
    public DateOnly GetLocalDateOnly() => DateOnly.FromDateTime(GetLocalDateTime());
    public DateTime GetUtcDateTime() => now;
    public DateTimeOffset GetUtcDateTimeOffset() => new DateTimeOffset(now);
    public DateOnly GetUtcDateOnly() => DateOnly.FromDateTime(now);
    public TimeZoneInfo GetLocalTimeZoneInfo() => _tz;
    public TimeZoneInfo GetTimeZoneInfo(string windowsOrIanaTimeZoneId) => _tz;
}