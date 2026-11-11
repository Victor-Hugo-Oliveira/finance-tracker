using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Persistence;
using Microsoft.JSInterop;

namespace FinanceTracker.Web.Services;

/// <summary>
/// Saves the whole <see cref="FinanceData"/> document in the browser's localStorage.
/// Data never leaves the user's device.
/// </summary>
public sealed class LocalStorageFinanceDataStore(IJSRuntime js, ILogger<LocalStorageFinanceDataStore> logger) : IFinanceDataStore
{
    public const string StorageKey = "finance-tracker:data";

    public async Task<FinanceData?> LoadAsync(CancellationToken ct = default)
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", ct, StorageKey);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return FinanceDataSerializer.Deserialize(json);
        }
        catch (DomainException ex)
        {
            // Never silently destroy data: keep the unreadable copy aside and start fresh.
            var backupKey = $"{StorageKey}:unreadable:{DateTime.UtcNow:yyyyMMddHHmmss}";
            await js.InvokeVoidAsync("localStorage.setItem", ct, backupKey, json);
            logger.LogWarning(ex, "Stored data could not be read; kept a copy under {Key}", backupKey);
            return null;
        }
    }

    public async Task SaveAsync(FinanceData data, CancellationToken ct = default) =>
        await js.InvokeVoidAsync("localStorage.setItem", ct, StorageKey, FinanceDataSerializer.Serialize(data));
}
