using FinanceTracker.Core.Domain;

namespace FinanceTracker.Core.Transfer;

/// <summary>
/// Checks a deserialized document before it replaces the user's data. JSON deserialization skips the
/// entity constructors, so a hand-edited or corrupted backup could otherwise sneak in broken data.
/// </summary>
public static class FinanceDataValidator
{
    /// <exception cref="DomainException">Describes the first problem found.</exception>
    public static void Validate(FinanceData data)
    {
        if (data.Categories is null || data.Transactions is null || data.Budgets is null)
            throw Invalid("faltam seções no arquivo");

        var categories = new Dictionary<Guid, Category>();
        foreach (var c in data.Categories)
        {
            if (c is null || c.Id == Guid.Empty || !categories.TryAdd(c.Id, c))
                throw Invalid("categoria sem identificador ou repetida");
            if (string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > Category.NameMaxLength)
                throw Invalid($"categoria com nome inválido");
            if (!Enum.IsDefined(c.Type))
                throw Invalid($"categoria \"{c.Name}\" com tipo inválido");
            Rebuild(() => new Category(c.Name, c.Type, c.Color, c.Icon), $"categoria \"{c.Name}\"");
        }

        var transactionIds = new HashSet<Guid>();
        foreach (var t in data.Transactions)
        {
            if (t is null || t.Id == Guid.Empty || !transactionIds.Add(t.Id))
                throw Invalid("transação sem identificador ou repetida");
            if (!categories.TryGetValue(t.CategoryId, out var category))
                throw Invalid($"a transação \"{t.Description}\" aponta para uma categoria que não existe");
            if (category.Type != t.Type)
                throw Invalid($"a transação \"{t.Description}\" não combina com o tipo da categoria \"{category.Name}\"");
            Rebuild(() => new Transaction(t.Type, t.Amount, t.Date, t.Description, t.CategoryId, t.CreatedAt),
                $"transação \"{t.Description}\"");
        }

        var budgetCategories = new HashSet<Guid>();
        foreach (var b in data.Budgets)
        {
            if (b is null || !budgetCategories.Add(b.CategoryId))
                throw Invalid("orçamento repetido");
            if (!categories.TryGetValue(b.CategoryId, out var category) || category.Type != TransactionType.Expense)
                throw Invalid("orçamento ligado a uma categoria inexistente ou de receitas");
            Rebuild(() => new Budget(b.CategoryId, b.MonthlyLimit), $"orçamento de \"{category.Name}\"");
        }
    }

    /// <summary>Runs the entity's own constructor rules on the loaded values.</summary>
    private static void Rebuild(Action create, string what)
    {
        try
        {
            create();
        }
        catch (DomainException ex)
        {
            throw Invalid($"{what}: {ex.Message.TrimEnd('.').ToLowerInvariant()}");
        }
    }

    private static DomainException Invalid(string detail) => new($"Backup inválido: {detail}.");
}
