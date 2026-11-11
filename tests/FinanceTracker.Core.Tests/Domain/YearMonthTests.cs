using FinanceTracker.Core.Domain;

namespace FinanceTracker.Core.Tests.Domain;

public class YearMonthTests
{
    [Fact]
    public void First_and_last_day_handle_month_lengths_and_leap_years()
    {
        Assert.Equal(new DateOnly(2028, 2, 29), new YearMonth(2028, 2).LastDay);
        Assert.Equal(new DateOnly(2026, 2, 28), new YearMonth(2026, 2).LastDay);
        Assert.Equal(new DateOnly(2026, 11, 1), new YearMonth(2026, 11).FirstDay);
    }

    [Fact]
    public void AddMonths_crosses_year_boundaries()
    {
        Assert.Equal(new YearMonth(2027, 1), new YearMonth(2026, 12).AddMonths(1));
        Assert.Equal(new YearMonth(2025, 12), new YearMonth(2026, 1).AddMonths(-1));
    }

    [Fact]
    public void Contains_only_dates_of_that_month()
    {
        var november = new YearMonth(2026, 11);

        Assert.True(november.Contains(new DateOnly(2026, 11, 30)));
        Assert.False(november.Contains(new DateOnly(2026, 12, 1)));
        Assert.False(november.Contains(new DateOnly(2025, 11, 15)));
    }

    [Fact]
    public void Compares_chronologically()
    {
        Assert.True(new YearMonth(2025, 12) < new YearMonth(2026, 1));
        Assert.True(new YearMonth(2026, 2) > new YearMonth(2026, 1));
    }

    [Fact]
    public void Round_trips_through_iso_text()
    {
        var month = new YearMonth(2026, 3);

        Assert.Equal("2026-03", month.ToString());
        Assert.Equal(month, YearMonth.Parse("2026-03"));
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("26-01")]
    [InlineData("abc")]
    [InlineData("")]
    public void TryParse_rejects_invalid_text(string text)
    {
        Assert.False(YearMonth.TryParse(text, out _));
    }

    [Fact]
    public void Invalid_month_number_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new YearMonth(2026, 0));
    }
}
