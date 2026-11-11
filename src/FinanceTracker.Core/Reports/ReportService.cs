using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Services;

namespace FinanceTracker.Core.Reports;

public record MonthSummary(YearMonth Month, decimal Income, decimal Expenses, int TransactionCount)
{
    public decimal Balance => Income - Expenses;

    /// <summary>Share of income that was not spent, e.g. 25.0 = saved 25%. Null when there was no income.</summary>
    public decimal? SavingsRate => Income == 0 ? null : decimal.Round(Balance / Income * 100m, 1);
}

public record CategoryTotal(Guid CategoryId, string CategoryName, string Color, decimal Total, decimal Percentage);

/// <summary>Read-only numbers for the dashboard. Everything is computed with LINQ over the in-memory data.</summary>
public class ReportService(FinanceWorkspace workspace)
{
    /// <summary>Balance of every transaction ever recorded (income minus expenses).</summary>
    public async Task<decimal> GetOverallBalanceAsync(CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        return data.Transactions.Sum(t => t.SignedAmount);
    }

    public async Task<MonthSummary> GetMonthSummaryAsync(YearMonth month, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        return Summarize(month, data.Transactions.Where(t => month.Contains(t.Date)));
    }

    /// <summary>
    /// Totals per category for one month and type, largest first. Percentages add up to exactly 100
    /// (the rounding difference goes to the largest slice), so pie charts and legends agree.
    /// </summary>
    public async Task<IReadOnlyList<CategoryTotal>> GetTotalsByCategoryAsync(
        YearMonth month, TransactionType type = TransactionType.Expense, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        var categories = data.Categories.ToDictionary(c => c.Id);

        var groups = data.Transactions
            .Where(t => t.Type == type && month.Contains(t.Date))
            .GroupBy(t => t.CategoryId)
            .Select(g => (CategoryId: g.Key, Total: g.Sum(t => t.Amount)))
            .OrderByDescending(g => g.Total)
            .ToList();

        var grandTotal = groups.Sum(g => g.Total);
        if (grandTotal == 0) return [];

        var result = groups
            .Select(g =>
            {
                var category = categories.GetValueOrDefault(g.CategoryId);
                return new CategoryTotal(
                    g.CategoryId,
                    category?.Name ?? "Sem categoria",
                    category?.Color ?? Category.DefaultColor,
                    g.Total,
                    decimal.Round(g.Total / grandTotal * 100m, 1));
            })
            .ToList();

        var roundingGap = 100m - result.Sum(r => r.Percentage);
        if (roundingGap != 0)
            result[0] = result[0] with { Percentage = result[0].Percentage + roundingGap };

        return result
            .OrderByDescending(r => r.Total)
            .ThenBy(r => r.CategoryName, TextSearch.Ordering)
            .ToList();
    }

    /// <summary>
    /// One summary per month for the <paramref name="months"/> months ending at <paramref name="lastMonth"/>,
    /// oldest first. Months without transactions are included with zeros, so charts have no gaps.
    /// </summary>
    public async Task<IReadOnlyList<MonthSummary>> GetTrendAsync(YearMonth lastMonth, int months = 6, CancellationToken ct = default)
    {
        if (months is < 1 or > 36)
            throw new ArgumentOutOfRangeException(nameof(months), "Use between 1 and 36 months.");

        var data = await workspace.GetDataAsync(ct);
        var firstMonth = lastMonth.AddMonths(-(months - 1));

        var byMonth = data.Transactions
            .Where(t => t.Date >= firstMonth.FirstDay && t.Date <= lastMonth.LastDay)
            .ToLookup(t => YearMonth.From(t.Date));

        return Enumerable.Range(0, months)
            .Select(i => firstMonth.AddMonths(i))
            .Select(m => Summarize(m, byMonth[m]))
            .ToList();
    }

    private static MonthSummary Summarize(YearMonth month, IEnumerable<Transaction> transactions)
    {
        var list = transactions as IList<Transaction> ?? transactions.ToList();
        return new MonthSummary(
            month,
            list.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
            list.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount),
            list.Count);
    }
}
