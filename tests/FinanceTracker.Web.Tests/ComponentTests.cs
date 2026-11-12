using Bunit;
using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Reports;
using FinanceTracker.Web.Components;
using FinanceTracker.Web.Components.Charts;
using FinanceTracker.Web.Services;

namespace FinanceTracker.Web.Tests;

public class FormatTests
{
    [Theory]
    [InlineData(1234.5, "R$ 1.234,50")]
    [InlineData(0, "R$ 0,00")]
    [InlineData(-10, "-R$ 10,00")]
    public void Money_uses_brazilian_format(decimal value, string expected) =>
        Assert.Equal(expected, Format.Money(value).Replace(' ', ' '));

    [Theory]
    [InlineData(0, "R$ 0")]
    [InlineData(850, "R$ 850")]
    [InlineData(1500, "R$ 1,5 mil")]
    [InlineData(2000000, "R$ 2 mi")]
    public void CompactMoney_shortens_axis_labels(decimal value, string expected) =>
        Assert.Equal(expected, Format.CompactMoney(value));

    [Fact]
    public void Month_names_are_portuguese_and_capitalized()
    {
        Assert.Equal("Março de 2026", Format.Month(new YearMonth(2026, 3)));
        Assert.Equal("mar/2026", Format.ShortMonth(new YearMonth(2026, 3)));
    }
}

public class DonutChartTests : WebTestContext
{
    private static DonutChart.Slice Slice(string label, decimal value, decimal percent) => new(label, value, "#123456", percent);

    [Fact]
    public void Draws_one_arc_per_slice_with_an_accessible_tooltip()
    {
        var cut = Render<DonutChart>(p => p
            .Add(c => c.Slices, [Slice("Moradia", 750m, 75m), Slice("Lazer", 250m, 25m)])
            .Add(c => c.CenterValue, "R$ 1.000,00"));

        var arcs = cut.FindAll("circle.donut-slice");
        Assert.Equal(2, arcs.Count);
        Assert.Contains("Moradia: R$", arcs[0].QuerySelector("title")!.TextContent);
        Assert.Contains("(75%)", arcs[0].QuerySelector("title")!.TextContent);
        Assert.Contains("R$ 1.000,00", cut.Markup);
    }

    [Fact]
    public void Arcs_are_proportional_and_separated_by_a_gap()
    {
        var cut = Render<DonutChart>(p => p.Add(c => c.Slices, [Slice("A", 3m, 75m), Slice("B", 1m, 25m)]));

        var lengths = cut.FindAll("circle.donut-slice")
            .Select(e => double.Parse(e.GetAttribute("stroke-dasharray")!.Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        var circumference = 2 * Math.PI * 80;
        Assert.Equal(circumference * 0.75 - 2, lengths[0], 1);
        Assert.Equal(circumference * 0.25 - 2, lengths[1], 1);
    }

    [Fact]
    public void Nothing_is_drawn_without_data() =>
        Assert.Empty(Render<DonutChart>(p => p.Add(c => c.Slices, [])).FindAll("circle.donut-slice"));
}

public class TrendChartTests : WebTestContext
{
    private static IReadOnlyList<MonthSummary> SixMonths() =>
        Enumerable.Range(0, 6)
            .Select(i => new MonthSummary(new YearMonth(2026, 6 + i), 5000m, 1000m * (i + 1), 3))
            .ToList();

    [Fact]
    public void Draws_a_column_per_month_with_the_selected_one_highlighted()
    {
        var cut = Render<TrendChart>(p => p
            .Add(c => c.Months, SixMonths())
            .Add(c => c.Highlight, new YearMonth(2026, 11)));

        Assert.Equal(6, cut.FindAll("g.trend-column").Count);
        var current = Assert.Single(cut.FindAll("g.trend-column.current"));
        Assert.Contains("nov", current.TextContent);
        Assert.Contains("Despesas: R$", current.QuerySelector("title")!.TextContent);
    }

    [Fact]
    public void Axis_uses_round_values_that_cover_the_highest_bar()
    {
        var cut = Render<TrendChart>(p => p.Add(c => c.Months, SixMonths()));

        // Highest value is 6.000 → ticks 0, 2 mil, 4 mil, 6 mil.
        var labels = cut.FindAll("text.trend-axis-label").Select(e => e.TextContent.Trim()).ToList();
        Assert.Contains("R$ 0", labels);
        Assert.Contains("R$ 6 mil", labels);
        Assert.DoesNotContain("R$ 7 mil", labels);
    }
}

public class SmallComponentTests : WebTestContext
{
    [Fact]
    public void CategoryChip_shows_name_and_marks_archived_categories()
    {
        var category = new Category("Lazer", TransactionType.Expense, "#AA00FF", "celebration");
        category.Archive();

        var cut = Render<CategoryChip>(p => p.Add(c => c.Category, category));

        Assert.Contains("Lazer", cut.Markup);
        Assert.NotNull(cut.Find("[title='Categoria arquivada']"));
    }

    [Fact]
    public void CategoryChip_without_category_says_so() =>
        Assert.Contains("Sem categoria", Render<CategoryChip>().Markup);

    [Fact]
    public void MonthSelector_moves_between_months_and_back_to_today()
    {
        YearMonth? changed = null;
        var cut = Render<MonthSelector>(p => p
            .Add(c => c.Value, new YearMonth(2026, 9))
            .Add(c => c.ValueChanged, m => changed = m));

        Assert.Contains("Setembro de 2026", cut.Markup);

        cut.Find("button[aria-label='Próximo mês']").Click();
        Assert.Equal(new YearMonth(2026, 10), changed);

        cut.Find("button[aria-label='Mês anterior']").Click();
        Assert.Equal(new YearMonth(2026, 8), changed);

        cut.FindAll("button").Single(b => b.TextContent.Contains("Mês atual")).Click();
        Assert.Equal(ThisMonth, changed);
    }

    [Fact]
    public void MonthSelector_hides_the_today_button_on_the_current_month()
    {
        var cut = Render<MonthSelector>(p => p.Add(c => c.Value, ThisMonth));

        var today = cut.FindAll("button").Single(b => b.TextContent.Contains("Mês atual"));
        Assert.Contains("visibility:hidden", today.GetAttribute("style"));
    }
}
