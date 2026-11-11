using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FinanceTracker.Core.Domain;

public sealed partial class Category
{
    public const int NameMaxLength = 40;
    public const int IconMaxLength = 60;
    public const string DefaultColor = "#607D8B";

    [JsonInclude] public Guid Id { get; private set; }
    [JsonInclude] public string Name { get; private set; } = string.Empty;

    /// <summary>Whether the category is for income or expenses. Fixed after creation.</summary>
    [JsonInclude] public TransactionType Type { get; private set; }

    /// <summary>Hex color like "#4CAF50", used in charts and badges.</summary>
    [JsonInclude] public string Color { get; private set; } = DefaultColor;

    /// <summary>Icon name understood by the UI.</summary>
    [JsonInclude] public string Icon { get; private set; } = string.Empty;

    /// <summary>Archived categories keep their history but can't receive new transactions.</summary>
    [JsonInclude] public bool IsArchived { get; private set; }

    [JsonConstructor]
    private Category() { }

    public Category(string name, TransactionType type, string color, string? icon = null)
    {
        Id = Guid.NewGuid();
        Type = type;
        Apply(name, color, icon);
    }

    public void Update(string name, string color, string? icon) => Apply(name, color, icon);

    public void Archive() => IsArchived = true;

    public void Restore() => IsArchived = false;

    private void Apply(string name, string color, string? icon)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            throw new DomainException("Informe o nome da categoria.", nameof(Name));
        if (name.Length > NameMaxLength)
            throw new DomainException($"O nome deve ter no máximo {NameMaxLength} caracteres.", nameof(Name));
        if (color is null || !HexColor().IsMatch(color))
            throw new DomainException("Cor inválida. Use o formato #RRGGBB.", nameof(Color));

        icon = icon?.Trim() ?? string.Empty;
        if (icon.Length > IconMaxLength)
            throw new DomainException("Ícone inválido.", nameof(Icon));

        Name = name;
        Color = color.ToUpperInvariant();
        Icon = icon;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}
