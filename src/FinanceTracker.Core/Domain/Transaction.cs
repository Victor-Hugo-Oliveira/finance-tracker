using System.Text.Json.Serialization;

namespace FinanceTracker.Core.Domain;

/// <summary>
/// A single income or expense. <see cref="Amount"/> is always positive; the
/// <see cref="Type"/> says which way the money moved.
/// </summary>
public sealed class Transaction
{
    public const int DescriptionMaxLength = 200;
    public const decimal MaxAmount = 1_000_000_000m;

    [JsonInclude] public Guid Id { get; private set; }
    [JsonInclude] public TransactionType Type { get; private set; }

    /// <summary>Money is always <c>decimal</c> (never double) and has at most 2 decimal places.</summary>
    [JsonInclude] public decimal Amount { get; private set; }

    [JsonInclude] public DateOnly Date { get; private set; }
    [JsonInclude] public string Description { get; private set; } = string.Empty;
    [JsonInclude] public Guid CategoryId { get; private set; }
    [JsonInclude] public DateTime CreatedAt { get; private set; }
    [JsonInclude] public DateTime? UpdatedAt { get; private set; }

    /// <summary>Positive for income, negative for expenses. Handy for balances.</summary>
    [JsonIgnore] public decimal SignedAmount => Type == TransactionType.Income ? Amount : -Amount;

    [JsonConstructor]
    private Transaction() { }

    public Transaction(TransactionType type, decimal amount, DateOnly date, string description, Guid categoryId, DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        CreatedAt = createdAtUtc;
        Apply(type, amount, date, description, categoryId);
    }

    public void Update(TransactionType type, decimal amount, DateOnly date, string description, Guid categoryId, DateTime updatedAtUtc)
    {
        Apply(type, amount, date, description, categoryId);
        UpdatedAt = updatedAtUtc;
    }

    private void Apply(TransactionType type, decimal amount, DateOnly date, string description, Guid categoryId)
    {
        if (!Enum.IsDefined(type))
            throw new DomainException("Tipo de transação inválido.", nameof(Type));
        if (amount <= 0)
            throw new DomainException("O valor deve ser maior que zero.", nameof(Amount));
        if (amount > MaxAmount)
            throw new DomainException("Valor muito alto.", nameof(Amount));
        if (decimal.Round(amount, 2) != amount)
            throw new DomainException("O valor pode ter no máximo 2 casas decimais.", nameof(Amount));

        description = description?.Trim() ?? string.Empty;
        if (description.Length == 0)
            throw new DomainException("Informe uma descrição.", nameof(Description));
        if (description.Length > DescriptionMaxLength)
            throw new DomainException($"A descrição deve ter no máximo {DescriptionMaxLength} caracteres.", nameof(Description));

        if (categoryId == Guid.Empty)
            throw new DomainException("Escolha uma categoria.", nameof(CategoryId));

        Type = type;
        Amount = amount;
        Date = date;
        Description = description;
        CategoryId = categoryId;
    }
}
