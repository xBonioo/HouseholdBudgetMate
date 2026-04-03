using HouseholdBudgetMate.Application.Shared;
using PublicHoliday;

namespace HouseholdBudgetMate.Application.Kernel.Extensions;

public static class DateTimeExtensions
{
    /// <summary>
    /// Extension to convert UTC time to Poland time
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns>Poland time for provided UTC time, or provided time if it's kind was not UTC</returns>
    /// <exception cref="ApplicationException">If system was unable to find TimeZoneInfo we are not able to convert time</exception>
    public static DateTime UtcToPolandTime(this DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
        {
            return dateTime;
        }

        // Retrieve Poland time zone information
        TimeZoneInfo polandTimeZone;
        try
        {
            polandTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            // For Linux use the IANA time zone identifier
            polandTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
        }
        catch (InvalidTimeZoneException)
        {
            throw new ApplicationException("Error while processing time");
        }

        var utcDateTimeOffset = new DateTimeOffset(dateTime, TimeSpan.Zero);

        var polandDateTimeOffset = TimeZoneInfo.ConvertTime(utcDateTimeOffset, polandTimeZone);

        return polandDateTimeOffset.DateTime;
    }
    
    /// <summary>
    /// Extension to convert UTC time to Poland time
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns>Poland time for provided UTC time, or provided time if it's kind was not UTC</returns>
    /// <exception cref="ApplicationException">If system was unable to find TimeZoneInfo we are not able to convert time</exception>
    public static DateTime? UtcToPolandTime(this DateTime? dateTime)
    {
        if (!dateTime.HasValue)
        {
            return null;
        }
        
        return dateTime.Value.UtcToPolandTime();
    }
}