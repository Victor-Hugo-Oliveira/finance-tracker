using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Persistence;

namespace FinanceTracker.Core.Transfer;

public record BackupSummary(int Categories, int Transactions, int Budgets);

/// <summary>Whole-data operations: CSV export, JSON backup/restore, sample data and reset.</summary>
public class BackupService(FinanceWorkspace workspace, TimeProvider timeProvider)
{
    public async Task<string> ExportCsvAsync(CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        return TransactionCsv.Export(data.Transactions, data.Categories.ToDictionary(c => c.Id));
    }

    public async Task<string> ExportJsonAsync(CancellationToken ct = default) =>
        FinanceDataSerializer.Serialize(await workspace.GetDataAsync(ct));

    public async Task<BackupSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        return Summarize(data);
    }

    /// <summary>Reads and checks a backup without applying it, so the user can confirm what will be restored.</summary>
    /// <exception cref="DomainException">The file is not a valid, consistent backup.</exception>
    public static (FinanceData Data, BackupSummary Summary) ReadBackup(string json)
    {
        var data = FinanceDataSerializer.Deserialize(json);
        FinanceDataValidator.Validate(data);
        return (data, Summarize(data));
    }

    /// <summary>Replaces everything with the backup. Current data is lost.</summary>
    public async Task<BackupSummary> RestoreAsync(string json, CancellationToken ct = default)
    {
        var (data, summary) = ReadBackup(json);
        await workspace.ReplaceAsync(data, ct);
        return summary;
    }

    /// <summary>Replaces everything with six months of realistic example data ending this month.</summary>
    public async Task LoadSampleDataAsync(CancellationToken ct = default) =>
        await workspace.ReplaceAsync(SampleData.Create(DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
            timeProvider.GetUtcNow().UtcDateTime), ct);

    /// <summary>Deletes all transactions and budgets and brings back the default categories.</summary>
    public Task ResetAsync(CancellationToken ct = default) => workspace.ReplaceAsync(FinanceData.CreateDefault(), ct);

    private static BackupSummary Summarize(FinanceData data) =>
        new(data.Categories.Count, data.Transactions.Count, data.Budgets.Count);
}
