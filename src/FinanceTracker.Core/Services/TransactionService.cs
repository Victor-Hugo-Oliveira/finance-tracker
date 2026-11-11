using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Persistence;

namespace FinanceTracker.Core.Services;

public record TransactionInput(
    TransactionType Type,
    decimal Amount,
    DateOnly Date,
    string Description,
    Guid CategoryId);

/// <summary>All filters are optional; combine as needed.</summary>
public record TransactionFilter
{
    public YearMonth? Month { get; init; }
    public TransactionType? Type { get; init; }
    public Guid? CategoryId { get; init; }

    /// <summary>Matched against the description, ignoring case and accents.</summary>
    public string? Search { get; init; }
}

public class TransactionService(FinanceWorkspace workspace, TimeProvider timeProvider)
{
    /// <summary>Matching transactions, newest first.</summary>
    public async Task<IReadOnlyList<Transaction>> QueryAsync(TransactionFilter? filter = null, CancellationToken ct = default)
    {
        filter ??= new TransactionFilter();
        var data = await workspace.GetDataAsync(ct);
        var search = filter.Search?.Trim();

        return data.Transactions
            .Where(t => filter.Month is not { } month || month.Contains(t.Date))
            .Where(t => filter.Type is null || t.Type == filter.Type)
            .Where(t => filter.CategoryId is null || t.CategoryId == filter.CategoryId)
            .Where(t => string.IsNullOrEmpty(search) || TextSearch.Contains(t.Description, search))
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ToList();
    }

    public async Task<Transaction> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        return Find(data, id);
    }

    public async Task<Transaction> CreateAsync(TransactionInput input, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        ValidateCategory(data, input, currentCategoryId: null);

        var transaction = new Transaction(
            input.Type, input.Amount, input.Date, input.Description, input.CategoryId, Now());
        data.Transactions.Add(transaction);
        await workspace.SaveAsync(ct);
        return transaction;
    }

    public async Task<Transaction> UpdateAsync(Guid id, TransactionInput input, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        var transaction = Find(data, id);
        ValidateCategory(data, input, currentCategoryId: transaction.CategoryId);

        transaction.Update(input.Type, input.Amount, input.Date, input.Description, input.CategoryId, Now());
        await workspace.SaveAsync(ct);
        return transaction;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        data.Transactions.Remove(Find(data, id));
        await workspace.SaveAsync(ct);
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;

    private static Transaction Find(FinanceData data, Guid id) =>
        data.Transactions.FirstOrDefault(t => t.Id == id) ?? throw new NotFoundException("Transação", id);

    private static void ValidateCategory(FinanceData data, TransactionInput input, Guid? currentCategoryId)
    {
        var category = data.Categories.FirstOrDefault(c => c.Id == input.CategoryId)
            ?? throw new DomainException("Escolha uma categoria válida.", nameof(TransactionInput.CategoryId));

        if (category.Type != input.Type)
            throw new DomainException(
                input.Type == TransactionType.Income
                    ? "Esta categoria é de despesas. Escolha uma categoria de receitas."
                    : "Esta categoria é de receitas. Escolha uma categoria de despesas.",
                nameof(TransactionInput.CategoryId));

        // Editing an old transaction may keep its archived category, but nothing new can go into one.
        if (category.IsArchived && category.Id != currentCategoryId)
            throw new DomainException("Esta categoria está arquivada.", nameof(TransactionInput.CategoryId));
    }
}
