using System.Text.Json.Serialization;

namespace FinanceTracker.Core.Domain;

/// <summary>Monthly spending limit for one expense category. The same limit applies to every month.</summary>
public sealed class Budget
{
    [JsonInclude] public Guid CategoryId { get; private set; }
    [JsonInclude] public decimal MonthlyLimit { get; private set; }

    [JsonConstructor]
    private Budget() { }

    public Budget(Guid categoryId, decimal monthlyLimit)
    {
        CategoryId = categoryId;
        SetLimit(monthlyLimit);
    }

    public void SetLimit(decimal monthlyLimit)
    {
        if (monthlyLimit <= 0)
            throw new DomainException("O limite deve ser maior que zero.", nameof(MonthlyLimit));
        if (monthlyLimit > Transaction.MaxAmount)
            throw new DomainException("Limite muito alto.", nameof(MonthlyLimit));
        if (decimal.Round(monthlyLimit, 2) != monthlyLimit)
            throw new DomainException("O limite pode ter no máximo 2 casas decimais.", nameof(MonthlyLimit));

        MonthlyLimit = monthlyLimit;
    }
}
