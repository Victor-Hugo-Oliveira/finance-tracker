using FinanceTracker.Core.Domain;

namespace FinanceTracker.Core.Persistence;

/// <summary>
/// Holds the user's data in memory for the app's lifetime and writes it back to the store
/// after each change. Services share one workspace, so they always see the same state.
/// </summary>
public sealed class FinanceWorkspace(IFinanceDataStore store)
{
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private FinanceData? _data;

    /// <summary>Raised after every successful save, so the UI can refresh.</summary>
    public event Action? Changed;

    public async Task<FinanceData> GetDataAsync(CancellationToken ct = default)
    {
        if (_data is not null) return _data;

        await _loadLock.WaitAsync(ct);
        try
        {
            if (_data is null)
            {
                var loaded = await store.LoadAsync(ct);
                if (loaded is null)
                {
                    loaded = FinanceData.CreateDefault();
                    await store.SaveAsync(loaded, ct);
                }
                _data = loaded;
            }
            return _data;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (_data is null) return;
        await store.SaveAsync(_data, ct);
        Changed?.Invoke();
    }

    /// <summary>Swaps everything for <paramref name="data"/> (restore from backup, sample data).</summary>
    public async Task ReplaceAsync(FinanceData data, CancellationToken ct = default)
    {
        _data = data;
        await SaveAsync(ct);
    }
}
