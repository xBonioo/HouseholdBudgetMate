using HouseholdBudgetMate.Abstractions.Contracts.Backup.Requests;

namespace HouseholdBudgetMate.Application.Services.Backup;

internal readonly record struct BackupPeriodRange(
    int FromYear,
    int FromMonth,
    int ToYear,
    int ToMonth)
{
    public static BackupPeriodRange? FromRequest(CreateBackupRequest request)
    {
        return request.FromYear.HasValue
               && request.FromMonth.HasValue
               && request.ToYear.HasValue
               && request.ToMonth.HasValue
            ? new BackupPeriodRange(
                request.FromYear.Value,
                request.FromMonth.Value,
                request.ToYear.Value,
                request.ToMonth.Value)
            : null;
    }

    public bool Contains(int year, int month)
    {
        var value = ToMonthIndex(year, month);
        return value >= ToMonthIndex(FromYear, FromMonth)
               && value <= ToMonthIndex(ToYear, ToMonth);
    }

    public bool ContainsYear(int year)
        => year >= FromYear && year <= ToYear;

    public bool Overlaps(DateOnly startDate, DateOnly endDate)
        => startDate <= EndDate && endDate >= StartDate;

    public bool StartsBeforeOrInRange(DateOnly effectiveDate)
        => effectiveDate <= EndDate;

    private DateOnly StartDate => new(FromYear, FromMonth, 1);

    private DateOnly EndDate => new(ToYear, ToMonth, DateTime.DaysInMonth(ToYear, ToMonth));

    private static int ToMonthIndex(int year, int month)
        => year * 12 + month;
}
