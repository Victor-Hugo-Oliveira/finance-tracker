using System.Globalization;

namespace FinanceTracker.Core.Services;

/// <summary>Search that ignores case and accents, so "cafe" finds "Café" and "SAUDE" finds "Saúde".</summary>
internal static class TextSearch
{
    private static readonly CompareInfo Compare = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
    private const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static bool Contains(string text, string term) => Compare.IndexOf(text, term, Options) >= 0;

    public static bool EqualsLoose(string a, string b) => Compare.Compare(a.Trim(), b.Trim(), Options) == 0;

    /// <summary>Alphabetical order for Portuguese ("Água" before "Banco").</summary>
    public static readonly StringComparer Ordering = StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true);
}
