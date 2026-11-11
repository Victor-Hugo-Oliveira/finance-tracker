using System.ComponentModel.DataAnnotations;
using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Services;

namespace FinanceTracker.Web.Pages.Transactions;

/// <summary>
/// What the form edits. Fields are nullable so an empty input shows "required" instead of 0.
/// These annotations give instant feedback; the Core still enforces every rule on save.
/// </summary>
public class TransactionFormModel
{
    public TransactionType Type { get; set; } = TransactionType.Expense;

    [Required(ErrorMessage = "Informe o valor.")]
    [Range(0.01, 1_000_000_000, ErrorMessage = "O valor deve ser maior que zero.")]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Informe a data.")]
    public DateTime? Date { get; set; }

    [Required(ErrorMessage = "Informe uma descrição.")]
    [MaxLength(Transaction.DescriptionMaxLength, ErrorMessage = "Descrição muito longa.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escolha uma categoria.")]
    public Guid? CategoryId { get; set; }

    public static TransactionFormModel From(Transaction t) => new()
    {
        Type = t.Type,
        Amount = t.Amount,
        Date = t.Date.ToDateTime(TimeOnly.MinValue),
        Description = t.Description,
        CategoryId = t.CategoryId
    };

    public TransactionInput ToInput() => new(
        Type,
        Amount!.Value,
        DateOnly.FromDateTime(Date!.Value),
        Description,
        CategoryId!.Value);
}
