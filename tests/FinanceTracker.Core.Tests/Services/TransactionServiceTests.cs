using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Services;

namespace FinanceTracker.Core.Tests.Services;

public class TransactionServiceTests
{
    private readonly TestApp _app = new();

    [Fact]
    public async Task Create_stores_the_transaction_with_the_clock_time()
    {
        var t = await _app.ExpenseAsync(42.90m, description: "Padaria");

        var stored = await _app.Transactions.GetByIdAsync(t.Id);
        Assert.Equal(42.90m, stored.Amount);
        Assert.Equal(TestApp.Now.UtcDateTime, stored.CreatedAt);
    }

    [Fact]
    public async Task Data_survives_a_reload()
    {
        var t = await _app.ExpenseAsync(42.90m, description: "Padaria");

        var reloaded = _app.Reload();
        var stored = await reloaded.Transactions.GetByIdAsync(t.Id);

        Assert.Equal("Padaria", stored.Description);
        Assert.Equal(42.90m, stored.Amount);
    }

    [Fact]
    public async Task Category_type_must_match_transaction_type()
    {
        var salary = await _app.CategoryAsync("Salário");

        var ex = await Assert.ThrowsAsync<DomainException>(() => _app.Transactions.CreateAsync(
            new TransactionInput(TransactionType.Expense, 10m, new DateOnly(2026, 11, 1), "Errado", salary.Id)));

        Assert.Equal(nameof(TransactionInput.CategoryId), ex.Field);
    }

    [Fact]
    public async Task Unknown_category_is_rejected()
    {
        await Assert.ThrowsAsync<DomainException>(() => _app.Transactions.CreateAsync(
            new TransactionInput(TransactionType.Expense, 10m, new DateOnly(2026, 11, 1), "x", Guid.NewGuid())));
    }

    [Fact]
    public async Task New_transactions_cannot_use_an_archived_category()
    {
        var leisure = await _app.CategoryAsync("Lazer");
        await _app.Categories.ArchiveAsync(leisure.Id);

        await Assert.ThrowsAsync<DomainException>(() => _app.ExpenseAsync(10m, "Lazer"));
    }

    [Fact]
    public async Task Editing_an_old_transaction_can_keep_its_archived_category()
    {
        var t = await _app.ExpenseAsync(10m, "Lazer");
        var leisure = await _app.CategoryAsync("Lazer");
        await _app.Categories.ArchiveAsync(leisure.Id);

        var updated = await _app.Transactions.UpdateAsync(t.Id,
            new TransactionInput(TransactionType.Expense, 15m, t.Date, "Cinema", leisure.Id));

        Assert.Equal(15m, updated.Amount);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Delete_removes_the_transaction()
    {
        var t = await _app.ExpenseAsync(10m);

        await _app.Transactions.DeleteAsync(t.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => _app.Transactions.GetByIdAsync(t.Id));
    }

    [Fact]
    public async Task Query_returns_newest_first()
    {
        await _app.ExpenseAsync(1m, date: new DateOnly(2026, 11, 1), description: "Primeira");
        await _app.ExpenseAsync(2m, date: new DateOnly(2026, 11, 20), description: "Última");
        await _app.ExpenseAsync(3m, date: new DateOnly(2026, 11, 10), description: "Meio");

        var result = await _app.Transactions.QueryAsync();

        Assert.Equal(["Última", "Meio", "Primeira"], result.Select(t => t.Description));
    }

    [Fact]
    public async Task Query_filters_by_month_type_and_category()
    {
        await _app.ExpenseAsync(10m, "Alimentação", new DateOnly(2026, 11, 5));
        await _app.ExpenseAsync(20m, "Transporte", new DateOnly(2026, 11, 6));
        await _app.ExpenseAsync(30m, "Alimentação", new DateOnly(2026, 10, 5));
        await _app.IncomeAsync(1000m, date: new DateOnly(2026, 11, 5));
        var food = await _app.CategoryAsync("Alimentação");

        Assert.Equal(3, (await _app.Transactions.QueryAsync(new() { Month = TestApp.ThisMonth })).Count);
        Assert.Single(await _app.Transactions.QueryAsync(new() { Type = TransactionType.Income }));
        Assert.Equal(2, (await _app.Transactions.QueryAsync(new() { CategoryId = food.Id })).Count);
        Assert.Single(await _app.Transactions.QueryAsync(new() { Month = TestApp.ThisMonth, CategoryId = food.Id }));
    }

    [Theory]
    [InlineData("cafe")]
    [InlineData("CAFÉ")]
    [InlineData("padaria")]
    public async Task Search_ignores_case_and_accents(string term)
    {
        await _app.ExpenseAsync(8m, description: "Café da padaria");
        await _app.ExpenseAsync(50m, description: "Mercado");

        var result = await _app.Transactions.QueryAsync(new() { Search = term });

        Assert.Equal("Café da padaria", Assert.Single(result).Description);
    }
}
