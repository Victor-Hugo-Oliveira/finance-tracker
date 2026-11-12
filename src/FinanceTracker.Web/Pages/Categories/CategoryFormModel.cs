using System.ComponentModel.DataAnnotations;
using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Services;

namespace FinanceTracker.Web.Pages.Categories;

/// <summary>What the category form edits. The Core re-validates everything on save.</summary>
public class CategoryFormModel
{
    [Required(ErrorMessage = "Informe o nome da categoria.")]
    [MaxLength(Category.NameMaxLength, ErrorMessage = "Nome muito longo.")]
    public string Name { get; set; } = string.Empty;

    public TransactionType Type { get; set; } = TransactionType.Expense;

    public string Color { get; set; } = CategoryPalette.Colors[0];

    public string Icon { get; set; } = "more_horiz";

    public static CategoryFormModel From(Category c) => new()
    {
        Name = c.Name,
        Type = c.Type,
        Color = c.Color,
        Icon = c.Icon
    };

    public CategoryInput ToInput() => new(Name, Type, Color, Icon);
}

/// <summary>Suggested colors, picked to read well on light and dark backgrounds.</summary>
public static class CategoryPalette
{
    public static readonly IReadOnlyList<string> Colors =
    [
        "#E53935", "#D81B60", "#8E24AA", "#5E35B1",
        "#3949AB", "#1E88E5", "#039BE5", "#00ACC1",
        "#00897B", "#43A047", "#7CB342", "#C0CA33",
        "#FDD835", "#FFB300", "#FB8C00", "#F4511E",
        "#6D4C41", "#757575", "#546E7A", "#607D8B"
    ];
}
