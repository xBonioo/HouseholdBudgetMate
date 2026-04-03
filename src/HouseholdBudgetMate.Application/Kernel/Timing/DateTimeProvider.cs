using Microsoft.Extensions.Options;
using HouseholdBudgetMate.Application.Kernel.Configurations;
using TimeZoneConverter;

namespace HouseholdBudgetMate.Application.Kernel.Timing;

public sealed class DateTimeProvider(IOptionsMonitor<ApplicationConfiguration> applicationConfiguration)
    : IDateTimeProvider
{
    /// <summary>
    ///     Represents the application configuration options monitor.
    /// </summary>
    private readonly IOptionsMonitor<ApplicationConfiguration> _applicationConfiguration = applicationConfiguration;

    public DateTime GetLocalDateTime()
    {
        return TimeZoneInfo.ConvertTime(DateTime.UtcNow, GetLocalTimeZoneInfo());
    }

    public DateTimeOffset GetLocalDateTimeOffset()
    {
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, GetLocalTimeZoneInfo());
    }

    public DateOnly GetLocalDateOnly()
    {
        return DateOnly.FromDateTime(GetLocalDateTime());
    }

    public DateTime GetUtcDateTime()
    {
        return DateTime.UtcNow;
    }

    public DateTimeOffset GetUtcDateTimeOffset()
    {
        return DateTimeOffset.UtcNow;
    }

    public DateOnly GetUtcDateOnly()
    {
        return DateOnly.FromDateTime(GetLocalDateTime());
    }

    public TimeZoneInfo GetLocalTimeZoneInfo()
    {
        return GetTimeZoneInfo(_applicationConfiguration.CurrentValue.Timezone);
    }

    public TimeZoneInfo GetTimeZoneInfo(string windowsOrIanaTimeZoneId)
    {
        return TZConvert.GetTimeZoneInfo(windowsOrIanaTimeZoneId);
    }
}