using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Persistence;

namespace FinanceTracker.Core.Tests.Persistence;

public class FinanceDataSerializerTests
{
    [Fact]
    public async Task Round_trip_preserves_every_field()
    {
        var app = new TestApp();
        var food = await app.CategoryAsync("Alimentação");
        await app.Categories.ArchiveAsync((await app.CategoryAsync("Lazer")).Id);
        await app.Budgets.SetAsync(food.Id, 750.25m);
        var original = await app.ExpenseAsync(123.45m, "Alimentação", new DateOnly(2026, 2, 28), "Feira");

        var data = FinanceDataSerializer.Deserialize(app.Store.Json!);

        var t = Assert.Single(data.Transactions);
        Assert.Equal(original.Id, t.Id);
        Assert.Equal(123.45m, t.Amount);
        Assert.Equal(new DateOnly(2026, 2, 28), t.Date);
        Assert.Equal(TransactionType.Expense, t.Type);
        Assert.Equal(food.Id, t.CategoryId);
        Assert.Equal(DateTimeKind.Utc, t.CreatedAt.Kind);
        Assert.Equal(750.25m, Assert.Single(data.Budgets).MonthlyLimit);
        Assert.True(data.Categories.Single(c => c.Name == "Lazer").IsArchived);
    }

    [Fact]
    public void Json_is_human_readable()
    {
        var json = FinanceDataSerializer.Serialize(FinanceData.CreateDefault());

        Assert.Contains("\"schemaVersion\":1", json);
        Assert.Contains("\"type\":\"Expense\"", json); // enums as names, not numbers
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":999}")]
    public void Invalid_or_newer_backups_are_rejected_with_a_friendly_message(string json)
    {
        var ex = Assert.Throws<DomainException>(() => FinanceDataSerializer.Deserialize(json));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }
}

public class FinanceWorkspaceTests
{
    [Fact]
    public async Task First_load_creates_and_saves_default_data()
    {
        var store = new InMemoryFinanceDataStore();

        var data = await new FinanceWorkspace(store).GetDataAsync();

        Assert.NotEmpty(data.Categories);
        Assert.NotNull(store.Json);
    }

    [Fact]
    public async Task Changed_event_fires_after_each_save()
    {
        var app = new TestApp();
        var count = 0;
        app.Workspace.Changed += () => count++;

        await app.ExpenseAsync(10m);
        await app.ExpenseAsync(20m);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Replace_swaps_all_data()
    {
        var app = new TestApp();
        await app.ExpenseAsync(10m);

        await app.Workspace.ReplaceAsync(FinanceData.CreateDefault());

        Assert.Empty(await app.Transactions.QueryAsync());
    }
}
