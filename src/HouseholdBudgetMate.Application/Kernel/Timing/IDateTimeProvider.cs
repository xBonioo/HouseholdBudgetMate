namespace HouseholdBudgetMate.Application.Kernel.Timing;

public interface IDateTimeProvider
{
    /// <summary>
    ///     Returns local DateTime object in configured timezone.
    /// </summary>
    /// <returns></returns>
    DateTime GetLocalDateTime();

    /// <summary>
    ///     Returns local DateTimeOffset object in configured timezone.
    /// </summary>
    /// <returns></returns>
    DateTimeOffset GetLocalDateTimeOffset();

    /// <summary>
    ///     Returns local DateOnly object in configured timezone.
    /// </summary>
    /// <returns></returns>
    DateOnly GetLocalDateOnly();

    /// <summary>
    ///     Returns utc DateTime object in configured timezone.
    /// </summary>
    /// <returns></returns>
    DateTime GetUtcDateTime();

    /// <summary>
    ///     Returns utc DateTimeOffset object in configured timezone.
    /// </summary>
    /// <returns></returns>
    DateTimeOffset GetUtcDateTimeOffset();

    /// <summary>
    ///     Returns utc DateOnly object in configured timezone.
    /// </summary>
    /// <returns></returns>
    DateOnly GetUtcDateOnly();

    /// <summary>
    ///     Returns TimeZoneInfo object for current timezone.
    /// </summary>
    /// <returns></returns>
    TimeZoneInfo GetLocalTimeZoneInfo();

    /// <summary>
    ///     Returns TimeZoneInfo object for given windows or iana timezone id.
    /// </summary>
    /// <param name="windowsOrIanaTimeZoneId"></param>
    /// <returns></returns>
    TimeZoneInfo GetTimeZoneInfo(string windowsOrIanaTimeZoneId);
}