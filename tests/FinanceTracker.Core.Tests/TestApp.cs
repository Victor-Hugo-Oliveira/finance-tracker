using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Reports;
using FinanceTracker.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace FinanceTracker.Core.Tests;

/// <summary>All services wired to one in-memory store and a fixed clock, like the real app.</summary>
public sealed class TestApp
{
    public static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    public static readonly YearMonth ThisMonth = new(2026, 11);

    public InMemoryFinanceDataStore Store { get; } = new();
    public FakeTimeProvider Clock { get; } = new(Now);
    public FinanceWorkspace Workspace { get; }
    public CategoryService Categories { get; }
    public TransactionService Transactions { get; }
    public BudgetService Budgets { get; }
    public ReportService Reports { get; }

    public TestApp()
    {
        Workspace = new FinanceWorkspace(Store);
        Categories = new CategoryService(Workspace);
        Transactions = new TransactionService(Workspace, Clock);
        Budgets = new BudgetService(Workspace);
        Reports = new ReportService(Workspace);
    }

    /// <summary>A fresh app reading the same store, i.e. "the user reloaded the page".</summary>
    public TestApp Reload()
    {
        var reloaded = new TestApp();
        reloaded.Store.SaveAsync(FinanceDataSerializer.Deserialize(Store.Json!)).GetAwaiter().GetResult();
        return reloaded;
    }

    public async Task<Category> CategoryAsync(string name)
    {
        var all = await Categories.GetAllAsync(includeArchived: true);
        return all.Single(c => c.Name == name);
    }

    public async Task<Transaction> ExpenseAsync(decimal amount, string category = "Alimentação", DateOnly? date = null, string description = "Despesa")
    {
        var cat = await CategoryAsync(category);
        return await Transactions.CreateAsync(new TransactionInput(
            TransactionType.Expense, amount, date ?? ThisMonth.FirstDay.AddDays(4), description, cat.Id));
    }

    public async Task<Transaction> IncomeAsync(decimal amount, string category = "Salário", DateOnly? date = null, string description = "Receita")
    {
        var cat = await CategoryAsync(category);
        return await Transactions.CreateAsync(new TransactionInput(
            TransactionType.Income, amount, date ?? ThisMonth.FirstDay.AddDays(4), description, cat.Id));
    }
}
