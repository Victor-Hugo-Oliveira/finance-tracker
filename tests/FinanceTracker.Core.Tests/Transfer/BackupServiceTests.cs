using System.Text.Json.Nodes;
using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Transfer;

namespace FinanceTracker.Core.Tests.Transfer;

public class BackupServiceTests
{
    private readonly TestApp _app = new();

    private async Task<string> BackupWithDataAsync()
    {
        await _app.ExpenseAsync(42.5m, "Alimentação");
        await _app.IncomeAsync(1000m);
        await _app.Budgets.SetAsync((await _app.CategoryAsync("Lazer")).Id, 300m);
        return await _app.Backup.ExportJsonAsync();
    }

    [Fact]
    public async Task Restore_replaces_everything_with_the_backup()
    {
        var json = await BackupWithDataAsync();
        var other = new TestApp();
        await other.ExpenseAsync(999m, "Transporte");

        var summary = await other.Backup.RestoreAsync(json);

        Assert.Equal((12, 2, 1), (summary.Categories, summary.Transactions, summary.Budgets));
        var transactions = await other.Transactions.QueryAsync();
        Assert.Equal(2, transactions.Count);
        Assert.DoesNotContain(transactions, t => t.Amount == 999m);
        Assert.Equal(2, (await other.Reload().Transactions.QueryAsync()).Count); // persisted
    }

    [Fact]
    public async Task Invalid_file_is_rejected_and_current_data_is_kept()
    {
        await _app.ExpenseAsync(10m);

        await Assert.ThrowsAsync<DomainException>(() => _app.Backup.RestoreAsync("{ isto não é json"));

        Assert.Single(await _app.Transactions.QueryAsync());
    }

    [Theory]
    [InlineData("orphan")]
    [InlineData("negative")]
    [InlineData("type-mismatch")]
    [InlineData("duplicate-id")]
    [InlineData("income-budget")]
    public async Task Inconsistent_backups_are_rejected(string problem)
    {
        var doc = JsonNode.Parse(await BackupWithDataAsync())!;
        var transactions = doc["transactions"]!.AsArray();
        var expense = transactions.First(t => (string?)t!["type"] == "Expense")!;

        switch (problem)
        {
            case "orphan": expense["categoryId"] = Guid.NewGuid().ToString(); break;
            case "negative": expense["amount"] = -5; break;
            case "type-mismatch": expense["type"] = "Income"; break;
            case "duplicate-id": transactions[1]!["id"] = (string?)transactions[0]!["id"]; break;
            case "income-budget":
                var salary = doc["categories"]!.AsArray().First(c => (string?)c!["name"] == "Salário")!;
                doc["budgets"]![0]!["categoryId"] = (string?)salary["id"];
                break;
        }

        var ex = Assert.Throws<DomainException>(() => BackupService.ReadBackup(doc.ToJsonString()));
        Assert.StartsWith("Backup inválido", ex.Message);
    }

    [Fact]
    public async Task Sample_data_has_six_months_of_valid_transactions_and_budgets()
    {
        await _app.ExpenseAsync(1m);

        await _app.Backup.LoadSampleDataAsync();

        var all = await _app.Transactions.QueryAsync();
        Assert.DoesNotContain(all, t => t.Amount == 1m); // replaced
        Assert.All(all, t => Assert.True(t.Date <= DateOnly.FromDateTime(TestApp.Now.Date)));
        Assert.Equal(6, all.Select(t => YearMonth.From(t.Date)).Distinct().Count());
        Assert.NotEmpty(await _app.Budgets.GetStatusAsync(TestApp.ThisMonth));

        // It must pass the same checks as a restored backup.
        BackupService.ReadBackup(await _app.Backup.ExportJsonAsync());
    }

    [Fact]
    public void Sample_data_is_deterministic()
    {
        var day = new DateOnly(2026, 11, 15);
        var created = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);

        var a = SampleData.Create(day, created).Transactions.Select(t => (t.Date, t.Amount, t.Description));
        var b = SampleData.Create(day, created).Transactions.Select(t => (t.Date, t.Amount, t.Description));

        Assert.Equal(a, b);
    }

    [Fact]
    public async Task Reset_brings_back_only_the_default_categories()
    {
        await BackupWithDataAsync();
        await _app.Categories.CreateAsync(new("Pets", TransactionType.Expense, "#795548"));

        await _app.Backup.ResetAsync();

        var summary = await _app.Backup.GetSummaryAsync();
        Assert.Equal((12, 0, 0), (summary.Categories, summary.Transactions, summary.Budgets));
    }
}
