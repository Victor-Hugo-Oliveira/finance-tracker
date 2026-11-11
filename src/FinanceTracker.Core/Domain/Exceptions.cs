namespace FinanceTracker.Core.Domain;

/// <summary>
/// A business rule was violated. The message is user-facing (pt-BR);
/// <see cref="Field"/> names the input it refers to, when there is one.
/// </summary>
public class DomainException(string message, string? field = null) : Exception(message)
{
    public string? Field { get; } = field;
}

public class NotFoundException(string entityName, Guid id)
    : Exception($"{entityName} não encontrada (id {id}).");
