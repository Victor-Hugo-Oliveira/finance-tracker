using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Services;

namespace FinanceTracker.Core.Tests.Services;

public class CategoryServiceTests
{
    private readonly TestApp _app = new();

    [Fact]
    public async Task First_use_starts_with_the_default_categories()
    {
        var categories = await _app.Categories.GetAllAsync();

        Assert.Equal(FinanceData.CreateDefault().Categories.Count, categories.Count);
    }

    [Fact]
    public async Task Categories_are_sorted_by_type_then_portuguese_alphabetical_order()
    {
        var expenses = await _app.Categories.GetAllAsync(TransactionType.Expense);
        var names = expenses.Select(c => c.Name).ToList();

        Assert.Equal(names.OrderBy(n => n, StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), true)), names);
        Assert.All(expenses, c => Assert.Equal(TransactionType.Expense, c.Type));
    }

    [Fact]
    public async Task Create_adds_a_category()
    {
        var pets = await _app.Categories.CreateAsync(new CategoryInput("Pets", TransactionType.Expense, "#795548", "pets"));

        Assert.Contains(await _app.Categories.GetAllAsync(), c => c.Id == pets.Id);
    }

    [Theory]
    [InlineData("Alimentação")]
    [InlineData("alimentacao")]
    [InlineData("  ALIMENTAÇÃO ")]
    public async Task Duplicate_names_are_rejected_ignoring_case_and_accents(string name)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _app.Categories.CreateAsync(new CategoryInput(name, TransactionType.Expense, "#000000")));
        Assert.Equal(nameof(Category.Name), ex.Field);
    }

    [Fact]
    public async Task Same_name_is_allowed_for_a_different_type()
    {
        var category = await _app.Categories.CreateAsync(new CategoryInput("Outras despesas", TransactionType.Income, "#000000"));

        Assert.Equal(TransactionType.Income, category.Type);
    }

    [Fact]
    public async Task Update_can_keep_its_own_name()
    {
        var food = await _app.CategoryAsync("Alimentação");

        var updated = await _app.Categories.UpdateAsync(food.Id, "Alimentação", "#FF0000", "restaurant");

        Assert.Equal("#FF0000", updated.Color);
    }

    [Fact]
    public async Task Archived_categories_are_hidden_unless_requested()
    {
        var leisure = await _app.CategoryAsync("Lazer");

        await _app.Categories.ArchiveAsync(leisure.Id);

        Assert.DoesNotContain(await _app.Categories.GetAllAsync(), c => c.Id == leisure.Id);
        Assert.Contains(await _app.Categories.GetAllAsync(includeArchived: true), c => c.Id == leisure.Id);
    }

    [Fact]
    public async Task Category_with_transactions_cannot_be_deleted()
    {
        await _app.ExpenseAsync(30m, "Lazer");
        var leisure = await _app.CategoryAsync("Lazer");

        await Assert.ThrowsAsync<DomainException>(() => _app.Categories.DeleteAsync(leisure.Id));
    }

    [Fact]
    public async Task Deleting_an_unused_category_also_removes_its_budget()
    {
        var leisure = await _app.CategoryAsync("Lazer");
        await _app.Budgets.SetAsync(leisure.Id, 200m);

        await _app.Categories.DeleteAsync(leisure.Id);

        Assert.Empty(await _app.Budgets.GetStatusAsync(TestApp.ThisMonth));
        await Assert.ThrowsAsync<NotFoundException>(() => _app.Categories.GetByIdAsync(leisure.Id));
    }
}
