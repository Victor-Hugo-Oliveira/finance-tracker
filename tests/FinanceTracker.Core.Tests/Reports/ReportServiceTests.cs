using FinanceTracker.Core.Domain;

namespace FinanceTracker.Core.Tests.Reports;

public class ReportServiceTests
{
    private readonly TestApp _app = new();

    [Fact]
    public async Task Month_summary_adds_income_and_expenses_of_that_month_only()
    {
        await _app.IncomeAsync(5000m);
        await _app.ExpenseAsync(1200.50m, "Moradia");
        await _app.ExpenseAsync(299.50m, "Alimentação");
        await _app.ExpenseAsync(999m, date: TestApp.ThisMonth.AddMonths(-1).FirstDay);

        var summary = await _app.Reports.GetMonthSummaryAsync(TestApp.ThisMonth);

        Assert.Equal(5000m, summary.Income);
        Assert.Equal(1500m, summary.Expenses);
        Assert.Equal(3500m, summary.Balance);
        Assert.Equal(70.0m, summary.SavingsRate);
        Assert.Equal(3, summary.TransactionCount);
    }

    [Fact]
    public async Task Savings_rate_is_null_without_income()
    {
        await _app.ExpenseAsync(100m);

        var summary = await _app.Reports.GetMonthSummaryAsync(TestApp.ThisMonth);

        Assert.Null(summary.SavingsRate);
        Assert.Equal(-100m, summary.Balance);
    }

    [Fact]
    public async Task Overall_balance_includes_every_month()
    {
        await _app.IncomeAsync(1000m, date: new DateOnly(2025, 1, 10));
        await _app.ExpenseAsync(300m, date: new DateOnly(2026, 11, 10));

        Assert.Equal(700m, await _app.Reports.GetOverallBalanceAsync());
    }

    [Fact]
    public async Task Totals_by_category_are_largest_first_with_percentages()
    {
        await _app.ExpenseAsync(600m, "Moradia");
        await _app.ExpenseAsync(300m, "Alimentação");
        await _app.ExpenseAsync(100m, "Alimentação");
        await _app.IncomeAsync(5000m); // income is not part of the expense breakdown

        var totals = await _app.Reports.GetTotalsByCategoryAsync(TestApp.ThisMonth);

        Assert.Equal(["Moradia", "Alimentação"], totals.Select(t => t.CategoryName));
        Assert.Equal([600m, 400m], totals.Select(t => t.Total));
        Assert.Equal([60.0m, 40.0m], totals.Select(t => t.Percentage));
    }

    [Fact]
    public async Task Percentages_always_add_up_to_exactly_100()
    {
        // Three equal slices: 33.3 + 33.3 + 33.3 = 99.9 without the rounding fix.
        await _app.ExpenseAsync(10m, "Moradia");
        await _app.ExpenseAsync(10m, "Alimentação");
        await _app.ExpenseAsync(10m, "Transporte");

        var totals = await _app.Reports.GetTotalsByCategoryAsync(TestApp.ThisMonth);

        Assert.Equal(100m, totals.Sum(t => t.Percentage));
    }

    [Fact]
    public async Task Totals_by_category_can_be_computed_for_income()
    {
        await _app.IncomeAsync(4000m, "Salário");
        await _app.IncomeAsync(1000m, "Freelance");

        var totals = await _app.Reports.GetTotalsByCategoryAsync(TestApp.ThisMonth, TransactionType.Income);

        Assert.Equal([80.0m, 20.0m], totals.Select(t => t.Percentage));
    }

    [Fact]
    public async Task Empty_month_has_no_category_totals()
    {
        Assert.Empty(await _app.Reports.GetTotalsByCategoryAsync(TestApp.ThisMonth));
    }

    [Fact]
    public async Task Trend_lists_every_month_oldest_first_including_empty_ones()
    {
        await _app.IncomeAsync(3000m, date: new DateOnly(2026, 9, 5));
        await _app.ExpenseAsync(500m, date: new DateOnly(2026, 11, 5));
        await _app.ExpenseAsync(999m, date: new DateOnly(2026, 5, 31)); // outside the 6-month window

        var trend = await _app.Reports.GetTrendAsync(TestApp.ThisMonth, months: 6);

        Assert.Equal(
            ["2026-06", "2026-07", "2026-08", "2026-09", "2026-10", "2026-11"],
            trend.Select(m => m.Month.ToString()));
        Assert.Equal(3000m, trend.Single(m => m.Month == new YearMonth(2026, 9)).Income);
        Assert.Equal(500m, trend.Last().Expenses);
        Assert.Equal(0, trend.Single(m => m.Month == new YearMonth(2026, 10)).TransactionCount);
        Assert.Equal(3000m - 500m, trend.Sum(m => m.Balance));
    }

    [Fact]
    public async Task Trend_rejects_unreasonable_ranges()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _app.Reports.GetTrendAsync(TestApp.ThisMonth, 0));
    }
}
