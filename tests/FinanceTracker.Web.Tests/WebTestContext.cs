using System.Globalization;
using Bunit;
using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Reports;
using FinanceTracker.Core.Services;
using FinanceTracker.Core.Transfer;
using FinanceTracker.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MudBlazor;
using MudBlazor.Services;

namespace FinanceTracker.Web.Tests;

/// <summary>
/// bUnit context wired like Program.cs, but with data in memory and a fixed clock
/// (15/11/2026), so pages render real services against predictable data.
/// </summary>
public abstract class WebTestContext : BunitContext
{
    public static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    public static readonly YearMonth ThisMonth = new(2026, 11);

    protected FakeTimeProvider Clock { get; } = new(Now);

    /// <summary>Where MudBlazor renders open lists and dialogs, for assertions about them.</summary>
    protected IRenderedComponent<MudPopoverProvider> Popovers { get; }
    protected IRenderedComponent<MudDialogProvider> Dialogs { get; }

    protected WebTestContext()
    {
        CultureInfo.CurrentCulture = Format.PtBr;
        CultureInfo.CurrentUICulture = Format.PtBr;

        JSInterop.Mode = JSRuntimeMode.Loose; // MudBlazor and localStorage calls just succeed
        Services.AddMudServices();
        Services.AddSingleton<TimeProvider>(Clock);
        Services.AddSingleton<IFinanceDataStore, InMemoryFinanceDataStore>();
        Services.AddSingleton<FinanceWorkspace>();
        Services.AddSingleton<CategoryService>();
        Services.AddSingleton<TransactionService>();
        Services.AddSingleton<BudgetService>();
        Services.AddSingleton<ReportService>();
        Services.AddSingleton<ImportService>();
        Services.AddSingleton<BackupService>();
        Services.AddScoped<FileDownloader>();
        Services.AddScoped<ThemePreference>();

        // Pages use selects, tooltips and dialogs, which need these providers on screen.
        Popovers = Render<MudPopoverProvider>();
        Dialogs = Render<MudDialogProvider>();
    }

    /// <summary>
    /// xUnit v2 disposes test classes synchronously, but MudBlazor's services only support async disposal.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    protected T Service<T>() where T : notnull => Services.GetRequiredService<T>();

    protected async Task<Category> CategoryAsync(string name) =>
        (await Service<CategoryService>().GetAllAsync(includeArchived: true)).Single(c => c.Name == name);

    protected async Task<Transaction> ExpenseAsync(decimal amount, string category = "Alimentação", string description = "Despesa")
    {
        var c = await CategoryAsync(category);
        return await Service<TransactionService>().CreateAsync(
            new TransactionInput(TransactionType.Expense, amount, new DateOnly(2026, 11, 5), description, c.Id));
    }

    protected async Task<Transaction> IncomeAsync(decimal amount, string description = "Salário")
    {
        var c = await CategoryAsync("Salário");
        return await Service<TransactionService>().CreateAsync(
            new TransactionInput(TransactionType.Income, amount, new DateOnly(2026, 11, 5), description, c.Id));
    }
}
