using FinanceTracker.Core.Transfer;
using MudBlazor;

namespace FinanceTracker.Web.Services;

/// <summary>
/// Converts between money values and what the user types in an amount field.
/// Phone keyboards often show only a dot, so "38.07" must mean R$ 38,07 (not 3.807):
/// a dot followed by one or two digits is read as the decimal mark, the same rule as the CSV import.
/// "1.234,56", "1234,56" and "R$ 10" keep working.
/// </summary>
public sealed class MoneyConverter : IReversibleConverter<decimal?, string?>
{
    public static readonly MoneyConverter Instance = new();

    public string? Convert(decimal? value) => value?.ToString("N2", Format.PtBr);

    public decimal? ConvertBack(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!TransactionCsv.TryParseAmount(text, out var amount))
            throw new FormatException("Valor inválido. Use, por exemplo, 38,07.");
        return amount;
    }
}
