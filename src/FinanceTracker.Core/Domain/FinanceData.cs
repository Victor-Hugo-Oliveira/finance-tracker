namespace FinanceTracker.Core.Domain;

/// <summary>
/// Everything the app stores for one person. Saved as a single JSON document
/// (browser storage, backup file), so it carries a schema version for future migrations.
/// </summary>
public sealed class FinanceData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<Category> Categories { get; init; } = [];
    public List<Transaction> Transactions { get; init; } = [];
    public List<Budget> Budgets { get; init; } = [];

    /// <summary>Starting point for a new user: empty, with a sensible set of categories.</summary>
    public static FinanceData CreateDefault() => new()
    {
        Categories =
        [
            new("Salário", TransactionType.Income, "#2E7D32", "work"),
            new("Freelance", TransactionType.Income, "#00897B", "laptop"),
            new("Investimentos", TransactionType.Income, "#1565C0", "trending_up"),
            new("Outras receitas", TransactionType.Income, "#5E35B1", "add_circle"),

            new("Moradia", TransactionType.Expense, "#6D4C41", "home"),
            new("Alimentação", TransactionType.Expense, "#EF6C00", "restaurant"),
            new("Transporte", TransactionType.Expense, "#0277BD", "directions_car"),
            new("Saúde", TransactionType.Expense, "#C62828", "favorite"),
            new("Educação", TransactionType.Expense, "#3949AB", "school"),
            new("Lazer", TransactionType.Expense, "#AD1457", "celebration"),
            new("Assinaturas", TransactionType.Expense, "#7B1FA2", "subscriptions"),
            new("Outras despesas", TransactionType.Expense, "#546E7A", "more_horiz")
        ]
    };
}
