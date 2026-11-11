using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Persistence;

namespace FinanceTracker.Core.Services;

public enum BudgetLevel
{
    /// <summary>Below 80% of the limit.</summary>
    Ok,

    /// <summary>From 80% up to the limit.</summary>
    Warning,

    /// <summary>Over the limit.</summary>
    Exceeded
}

public record BudgetStatus(
    Guid CategoryId,
    string CategoryName,
    string CategoryColor,
    decimal Limit,
    decimal Spent,
    decimal Remaining,
    decimal PercentUsed,
    BudgetLevel Level);

public class BudgetService(FinanceWorkspace workspace)
{
    public const decimal WarningThresholdPercent = 80m;

    /// <summary>Creates or changes the monthly limit of an expense category.</summary>
    public async Task<Budget> SetAsync(Guid categoryId, decimal monthlyLimit, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        var category = CategoryService.Find(data, categoryId);

        if (category.Type != TransactionType.Expense)
            throw new DomainException("Orçamentos só valem para categorias de despesas.");
        if (category.IsArchived)
            throw new DomainException("Esta categoria está arquivada.");

        var budget = data.Budgets.FirstOrDefault(b => b.CategoryId == categoryId);
        if (budget is null)
        {
            budget = new Budget(categoryId, monthlyLimit);
            data.Budgets.Add(budget);
        }
        else
        {
            budget.SetLimit(monthlyLimit);
        }

        await workspace.SaveAsync(ct);
        return budget;
    }

    public async Task RemoveAsync(Guid categoryId, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        if (data.Budgets.RemoveAll(b => b.CategoryId == categoryId) > 0)
            await workspace.SaveAsync(ct);
    }

    /// <summary>How much of each budget was used in <paramref name="month"/>, most used first.</summary>
    public async Task<IReadOnlyList<BudgetStatus>> GetStatusAsync(YearMonth month, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);

        var spentByCategory = data.Transactions
            .Where(t => t.Type == TransactionType.Expense && month.Contains(t.Date))
            .GroupBy(t => t.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        return data.Budgets
            .Join(data.Categories.Where(c => !c.IsArchived), b => b.CategoryId, c => c.Id, (b, c) => (Budget: b, Category: c))
            .Select(x =>
            {
                var spent = spentByCategory.GetValueOrDefault(x.Category.Id);
                var percent = decimal.Round(spent / x.Budget.MonthlyLimit * 100m, 1);
                var level = spent > x.Budget.MonthlyLimit ? BudgetLevel.Exceeded
                    : percent >= WarningThresholdPercent ? BudgetLevel.Warning
                    : BudgetLevel.Ok;

                return new BudgetStatus(
                    x.Category.Id, x.Category.Name, x.Category.Color,
                    x.Budget.MonthlyLimit, spent, x.Budget.MonthlyLimit - spent, percent, level);
            })
            .OrderByDescending(s => s.PercentUsed)
            .ThenBy(s => s.CategoryName, TextSearch.Ordering)
            .ToList();
    }
}
