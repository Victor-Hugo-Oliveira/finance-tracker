using System.Globalization;
using FinanceTracker.Core.Domain;

namespace FinanceTracker.Web.Services;

/// <summary>Brazilian display formats used across the UI.</summary>
public static class Format
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"R$ 1.234,56"</summary>
    public static string Money(decimal value) => value.ToString("C", PtBr);

    /// <summary>"+ R$ 10,00" for income, "− R$ 10,00" for expenses.</summary>
    public static string SignedMoney(Transaction t) =>
        (t.Type == TransactionType.Income ? "+ " : "− ") + Money(t.Amount);

    /// <summary>"15/11/2026"</summary>
    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", PtBr);

    /// <summary>"Novembro de 2026"</summary>
    public static string Month(YearMonth month)
    {
        var text = month.FirstDay.ToString("MMMM 'de' yyyy", PtBr);
        return char.ToUpper(text[0], PtBr) + text[1..];
    }

    public static string TypeName(TransactionType type) =>
        type == TransactionType.Income ? "Receita" : "Despesa";
}
