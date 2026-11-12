using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Services;

namespace FinanceTracker.Core.Transfer;

public record NewCategory(string Name, TransactionType Type);

/// <summary>What an import would do, shown to the user before anything is saved.</summary>
public record ImportPreview(
    IReadOnlyList<CsvTransactionRow> ToImport,
    IReadOnlyList<CsvLineError> Errors,
    IReadOnlyList<CsvTransactionRow> Duplicates,
    IReadOnlyList<NewCategory> NewCategories)
{
    public decimal TotalIncome => ToImport.Where(r => r.Type == TransactionType.Income).Sum(r => r.Amount);
    public decimal TotalExpenses => ToImport.Where(r => r.Type == TransactionType.Expense).Sum(r => r.Amount);
}

/// <summary>
/// Imports transactions from CSV in two steps: <see cref="PreviewAsync"/> validates every line and
/// reports problems without touching data; <see cref="ImportAsync"/> then saves the valid rows at once.
/// </summary>
public class ImportService(FinanceWorkspace workspace, TimeProvider timeProvider)
{
    public async Task<ImportPreview> PreviewAsync(string csvText, CancellationToken ct = default)
    {
        var parsed = TransactionCsv.Parse(csvText);
        var data = await workspace.GetDataAsync(ct);

        var errors = parsed.Errors.ToList();
        var toImport = new List<CsvTransactionRow>();
        var duplicates = new List<CsvTransactionRow>();
        var newCategories = new List<NewCategory>();

        // How many times each transaction already exists, so importing the same file twice doesn't double
        // everything. Counted (not a set): two identical coffees on the same day in one file are both real.
        var categoryNames = data.Categories.ToDictionary(c => c.Id, c => c.Name);
        var existing = data.Transactions
            .GroupBy(t => Fingerprint(t.Type, t.Amount, t.Date, t.Description, categoryNames.GetValueOrDefault(t.CategoryId) ?? ""))
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var row in parsed.Rows)
        {
            var category = FindCategory(data, row.CategoryName, row.Type);
            if (category is { IsArchived: true })
            {
                errors.Add(new CsvLineError(row.Line, $"A categoria \"{category.Name}\" está arquivada. Restaure-a antes de importar."));
                continue;
            }

            var fingerprint = Fingerprint(row.Type, row.Amount, row.Date, row.Description, category?.Name ?? row.CategoryName);
            if (existing.GetValueOrDefault(fingerprint) > 0)
            {
                existing[fingerprint]--;
                duplicates.Add(row);
                continue;
            }

            if (category is null && !newCategories.Any(n => n.Type == row.Type && TextSearch.EqualsLoose(n.Name, row.CategoryName)))
                newCategories.Add(new NewCategory(row.CategoryName.Trim(), row.Type));

            toImport.Add(row);
        }

        return new ImportPreview(toImport, errors.OrderBy(e => e.Line).ToList(), duplicates, newCategories);
    }

    /// <summary>Saves the rows of a preview, creating missing categories. All or nothing.</summary>
    /// <returns>How many transactions were added.</returns>
    public async Task<int> ImportAsync(ImportPreview preview, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Build everything first; only touch the data once nothing can fail anymore.
        var createdCategories = new List<Category>();
        var transactions = new List<Transaction>();
        foreach (var row in preview.ToImport)
        {
            var category = FindCategory(data, row.CategoryName, row.Type)
                           ?? createdCategories.FirstOrDefault(c => c.Type == row.Type && TextSearch.EqualsLoose(c.Name, row.CategoryName));
            if (category is null)
            {
                category = new Category(row.CategoryName, row.Type, Category.DefaultColor, "label");
                createdCategories.Add(category);
            }
            if (category.IsArchived)
                throw new DomainException($"A categoria \"{category.Name}\" foi arquivada. Gere a prévia novamente.");

            transactions.Add(new Transaction(row.Type, row.Amount, row.Date, row.Description, category.Id, now));
        }

        data.Categories.AddRange(createdCategories);
        data.Transactions.AddRange(transactions);
        await workspace.SaveAsync(ct);
        return transactions.Count;
    }

    private static Category? FindCategory(FinanceData data, string name, TransactionType type) =>
        data.Categories.FirstOrDefault(c => c.Type == type && TextSearch.EqualsLoose(c.Name, name));

    private static string Fingerprint(TransactionType type, decimal amount, DateOnly date, string description, string category) =>
        string.Join('|', type, amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            date.ToString("yyyy-MM-dd"), description.Trim().ToUpperInvariant(), category.Trim().ToUpperInvariant());
}
