using System.Globalization;

namespace FinanceTracker.Core.Domain;

/// <summary>A calendar month (e.g. 2026-11). .NET has no built-in type for this.</summary>
public readonly record struct YearMonth : IComparable<YearMonth>
{
    public int Year { get; }
    public int Month { get; }

    public YearMonth(int year, int month)
    {
        if (year is < 1 or > 9999) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        Year = year;
        Month = month;
    }

    public static YearMonth From(DateOnly date) => new(date.Year, date.Month);

    public static YearMonth From(DateTime date) => new(date.Year, date.Month);

    public DateOnly FirstDay => new(Year, Month, 1);
    public DateOnly LastDay => FirstDay.AddMonths(1).AddDays(-1);

    public bool Contains(DateOnly date) => date.Year == Year && date.Month == Month;

    public YearMonth AddMonths(int months) => From(FirstDay.AddMonths(months));

    public int CompareTo(YearMonth other) => (Year * 12 + Month).CompareTo(other.Year * 12 + other.Month);

    public static bool operator <(YearMonth a, YearMonth b) => a.CompareTo(b) < 0;
    public static bool operator >(YearMonth a, YearMonth b) => a.CompareTo(b) > 0;
    public static bool operator <=(YearMonth a, YearMonth b) => a.CompareTo(b) <= 0;
    public static bool operator >=(YearMonth a, YearMonth b) => a.CompareTo(b) >= 0;

    /// <summary>ISO format, e.g. "2026-11". Used in URLs, storage and CSV.</summary>
    public override string ToString() => $"{Year:D4}-{Month:D2}";

    public static YearMonth Parse(string value) =>
        TryParse(value, out var result) ? result : throw new FormatException($"Invalid month '{value}'. Expected yyyy-MM.");

    public static bool TryParse(string? value, out YearMonth result)
    {
        if (DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            result = From(date);
            return true;
        }

        result = default;
        return false;
    }
}
