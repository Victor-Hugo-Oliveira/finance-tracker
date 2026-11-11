using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Services;

namespace FinanceTracker.Core.Tests.Services;

public class BudgetServiceTests
{
    private readonly TestApp _app = new();

    private async Task<Guid> FoodBudgetAsync(decimal limit)
    {
        var food = await _app.CategoryAsync("Alimentação");
        await _app.Budgets.SetAsync(food.Id, limit);
        return food.Id;
    }

    [Theory]
    [InlineData(100, BudgetLevel.Ok)]       // 50%
    [InlineData(160, BudgetLevel.Warning)]  // 80% exactly
    [InlineData(200, BudgetLevel.Warning)]  // 100% exactly: at the limit, not over
    [InlineData(201, BudgetLevel.Exceeded)] // over the limit
    public async Task Level_depends_on_how_much_of_the_limit_was_spent(decimal spent, BudgetLevel expected)
    {
        await FoodBudgetAsync(200m);
        await _app.ExpenseAsync(spent, "Alimentação");

        var status = Assert.Single(await _app.Budgets.GetStatusAsync(TestApp.ThisMonth));

        Assert.Equal(expected, status.Level);
        Assert.Equal(200m - spent, status.Remaining);
    }

    [Fact]
    public async Task Only_expenses_of_that_month_count()
    {
        await FoodBudgetAsync(500m);
        await _app.ExpenseAsync(100m, "Alimentação");
        await _app.ExpenseAsync(999m, "Alimentação", TestApp.ThisMonth.AddMonths(-1).FirstDay);
        await _app.ExpenseAsync(999m, "Transporte");

        var status = Assert.Single(await _app.Budgets.GetStatusAsync(TestApp.ThisMonth));

        Assert.Equal(100m, status.Spent);
        Assert.Equal(20.0m, status.PercentUsed);
    }

    [Fact]
    public async Task Setting_again_updates_the_existing_limit()
    {
        await FoodBudgetAsync(200m);
        await FoodBudgetAsync(350m);

        var status = Assert.Single(await _app.Budgets.GetStatusAsync(TestApp.ThisMonth));
        Assert.Equal(350m, status.Limit);
    }

    [Fact]
    public async Task Income_categories_cannot_have_budgets()
    {
        var salary = await _app.CategoryAsync("Salário");

        await Assert.ThrowsAsync<DomainException>(() => _app.Budgets.SetAsync(salary.Id, 100m));
    }

    [Fact]
    public async Task Budgets_of_archived_categories_are_not_reported()
    {
        var foodId = await FoodBudgetAsync(200m);

        await _app.Categories.ArchiveAsync(foodId);

        Assert.Empty(await _app.Budgets.GetStatusAsync(TestApp.ThisMonth));
    }

    [Fact]
    public async Task Status_is_sorted_by_most_used_first()
    {
        await FoodBudgetAsync(100m);
        var transport = await _app.CategoryAsync("Transporte");
        await _app.Budgets.SetAsync(transport.Id, 100m);
        await _app.ExpenseAsync(10m, "Alimentação");
        await _app.ExpenseAsync(90m, "Transporte");

        var statuses = await _app.Budgets.GetStatusAsync(TestApp.ThisMonth);

        Assert.Equal(["Transporte", "Alimentação"], statuses.Select(s => s.CategoryName));
    }

    [Fact]
    public async Task Remove_deletes_the_budget()
    {
        var foodId = await FoodBudgetAsync(200m);

        await _app.Budgets.RemoveAsync(foodId);

        Assert.Empty(await _app.Budgets.GetStatusAsync(TestApp.ThisMonth));
    }
}
