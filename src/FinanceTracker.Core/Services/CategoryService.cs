using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Persistence;

namespace FinanceTracker.Core.Services;

public record CategoryInput(string Name, TransactionType Type, string Color, string? Icon = null);

public class CategoryService(FinanceWorkspace workspace)
{
    /// <summary>Categories sorted by type, then name. Archived ones only when asked.</summary>
    public async Task<IReadOnlyList<Category>> GetAllAsync(
        TransactionType? type = null, bool includeArchived = false, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        return data.Categories
            .Where(c => type is null || c.Type == type)
            .Where(c => includeArchived || !c.IsArchived)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name, TextSearch.Ordering)
            .ToList();
    }

    public async Task<Category> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        return Find(data, id);
    }

    public async Task<Category> CreateAsync(CategoryInput input, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        EnsureUniqueName(data, input.Name, input.Type, exceptId: null);

        var category = new Category(input.Name, input.Type, input.Color, input.Icon);
        data.Categories.Add(category);
        await workspace.SaveAsync(ct);
        return category;
    }

    /// <summary>Changes name, color and icon. The type can't change: it would invalidate existing transactions.</summary>
    public async Task<Category> UpdateAsync(Guid id, string name, string color, string? icon, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        var category = Find(data, id);
        EnsureUniqueName(data, name, category.Type, exceptId: id);

        category.Update(name, color, icon);
        await workspace.SaveAsync(ct);
        return category;
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        Find(data, id).Archive();
        await workspace.SaveAsync(ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        Find(data, id).Restore();
        await workspace.SaveAsync(ct);
    }

    /// <summary>Only categories without transactions can be deleted; others must be archived to keep history.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var data = await workspace.GetDataAsync(ct);
        var category = Find(data, id);

        if (data.Transactions.Any(t => t.CategoryId == id))
            throw new DomainException("Esta categoria tem transações. Arquive-a em vez de excluir.");

        data.Categories.Remove(category);
        data.Budgets.RemoveAll(b => b.CategoryId == id);
        await workspace.SaveAsync(ct);
    }

    internal static Category Find(FinanceData data, Guid id) =>
        data.Categories.FirstOrDefault(c => c.Id == id) ?? throw new NotFoundException("Categoria", id);

    private static void EnsureUniqueName(FinanceData data, string name, TransactionType type, Guid? exceptId)
    {
        // "alimentacao" and "Alimentação" count as the same name.
        var duplicate = data.Categories.Any(c =>
            c.Type == type && c.Id != exceptId && TextSearch.EqualsLoose(c.Name, name ?? string.Empty));

        if (duplicate)
            throw new DomainException("Já existe uma categoria com este nome.", nameof(Category.Name));
    }
}
