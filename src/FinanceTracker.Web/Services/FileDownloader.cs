using Microsoft.JSInterop;

namespace FinanceTracker.Web.Services;

/// <summary>Lets the user save generated files (CSV, backups) through the browser.</summary>
public sealed class FileDownloader(IJSRuntime js)
{
    public async Task DownloadAsync(string fileName, string content, string mimeType) =>
        await js.InvokeVoidAsync("financeTracker.download", fileName, content, mimeType);
}
