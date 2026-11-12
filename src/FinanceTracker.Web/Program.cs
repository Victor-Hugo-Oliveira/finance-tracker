using System.Globalization;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Reports;
using FinanceTracker.Core.Services;
using FinanceTracker.Core.Transfer;
using FinanceTracker.Web;
using FinanceTracker.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

// The app is Brazilian: R$, dd/MM/yyyy and Portuguese month names, whatever the browser language.
CultureInfo.DefaultThreadCurrentCulture = Format.PtBr;
CultureInfo.DefaultThreadCurrentUICulture = Format.PtBr;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.VisibleStateDuration = 3000;
});

// In WebAssembly the app runs for one user in one tab, so singletons hold that user's state.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IFinanceDataStore, LocalStorageFinanceDataStore>();
builder.Services.AddSingleton<FinanceWorkspace>();
builder.Services.AddSingleton<CategoryService>();
builder.Services.AddSingleton<TransactionService>();
builder.Services.AddSingleton<BudgetService>();
builder.Services.AddSingleton<ReportService>();
builder.Services.AddSingleton<ImportService>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddScoped<FileDownloader>();
builder.Services.AddScoped<ThemePreference>();

await builder.Build().RunAsync();
