using System.Globalization;
using System.Text.RegularExpressions;
using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Services;

namespace FinanceTracker.Core.Transfer;

/// <summary>A CSV line that was read successfully. The category is still a name; it's resolved on import.</summary>
public record CsvTransactionRow(int Line, TransactionType Type, decimal Amount, DateOnly Date, string Description, string CategoryName);

public record CsvLineError(int Line, string Message);

public record CsvParseResult(IReadOnlyList<CsvTransactionRow> Rows, IReadOnlyList<CsvLineError> Errors);

/// <summary>
/// Converts transactions to and from CSV in the Brazilian spreadsheet style:
/// <c>data;tipo;valor;categoria;descricao</c> with dates as dd/MM/yyyy and decimal commas.
/// </summary>
public static partial class TransactionCsv
{
    public const int MaxRows = 10_000;
    public const string DefaultIncomeCategory = "Outras receitas";
    public const string DefaultExpenseCategory = "Outras despesas";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Header = ["data", "tipo", "valor", "categoria", "descricao"];

    // Accepted header names for each column (compared ignoring case and accents).
    private static readonly Dictionary<string, string[]> ColumnAliases = new()
    {
        ["data"] = ["data", "date"],
        ["tipo"] = ["tipo", "type"],
        ["valor"] = ["valor", "amount", "value", "quantia"],
        ["categoria"] = ["categoria", "category"],
        ["descricao"] = ["descricao", "descrição", "description", "historico", "histórico"]
    };

    public static string Export(IEnumerable<Transaction> transactions, IReadOnlyDictionary<Guid, Category> categories)
    {
        var rows = new List<IReadOnlyList<string>> { Header };
        rows.AddRange(transactions
            .OrderBy(t => t.Date).ThenBy(t => t.CreatedAt)
            .Select(t => (IReadOnlyList<string>)
            [
                t.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                t.Type == TransactionType.Income ? "Receita" : "Despesa",
                t.Amount.ToString("0.00", PtBr),
                categories.GetValueOrDefault(t.CategoryId)?.Name ?? "",
                t.Description
            ]));
        return Csv.Write(rows);
    }

    public static CsvParseResult Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Fail(1, "O arquivo está vazio.");

        var lines = Csv.Read(text, Csv.DetectSeparator(text));
        if (lines.Count == 0)
            return Fail(1, "O arquivo está vazio.");

        var (headerLine, headerFields) = lines[0];
        var columns = MapColumns(headerFields);
        var missing = new[] { "data", "valor", "descricao" }.Where(c => !columns.ContainsKey(c)).ToList();
        if (missing.Count > 0)
            return Fail(headerLine, $"Cabeçalho sem a(s) coluna(s): {string.Join(", ", missing)}. " +
                                    "Use: data;tipo;valor;categoria;descricao");

        if (lines.Count - 1 > MaxRows)
            return Fail(headerLine, string.Format(PtBr, "Arquivo grande demais: o limite é {0:N0} linhas.", MaxRows));

        var rows = new List<CsvTransactionRow>();
        var errors = new List<CsvLineError>();
        foreach (var (line, fields) in lines.Skip(1))
        {
            var error = TryParseRow(line, fields, columns, out var row);
            if (error is not null) errors.Add(new CsvLineError(line, error));
            else rows.Add(row!);
        }
        return new CsvParseResult(rows, errors);
    }

    private static CsvParseResult Fail(int line, string message) => new([], [new CsvLineError(line, message)]);

    private static Dictionary<string, int> MapColumns(List<string> header)
    {
        var map = new Dictionary<string, int>();
        for (var i = 0; i < header.Count; i++)
        {
            var name = header[i].Trim();
            var key = ColumnAliases.FirstOrDefault(a => a.Value.Any(alias => TextSearch.EqualsLoose(alias, name))).Key;
            if (key is not null && !map.ContainsKey(key)) map[key] = i;
        }
        return map;
    }

    private static string? TryParseRow(int line, List<string> fields, Dictionary<string, int> columns, out CsvTransactionRow? row)
    {
        row = null;
        string Get(string column) =>
            columns.TryGetValue(column, out var i) && i < fields.Count ? fields[i].Trim() : string.Empty;

        if (!TryParseDate(Get("data"), out var date))
            return $"Data inválida: \"{Get("data")}\". Use dd/mm/aaaa.";

        if (!TryParseAmount(Get("valor"), out var amount))
            return $"Valor inválido: \"{Get("valor")}\".";

        TransactionType type;
        var typeText = Get("tipo");
        if (typeText.Length == 0)
        {
            // No type column (common in bank statements): the sign decides.
            type = amount < 0 ? TransactionType.Expense : TransactionType.Income;
        }
        else if (!TryParseType(typeText, out type))
        {
            return $"Tipo inválido: \"{typeText}\". Use Receita ou Despesa.";
        }
        amount = Math.Abs(amount);

        if (amount == 0) return "O valor deve ser maior que zero.";
        if (amount > Transaction.MaxAmount) return "Valor muito alto.";
        if (decimal.Round(amount, 2) != amount) return "O valor pode ter no máximo 2 casas decimais.";

        var description = Get("descricao");
        if (description.Length == 0) return "Descrição vazia.";
        if (description.Length > Transaction.DescriptionMaxLength)
            return $"Descrição com mais de {Transaction.DescriptionMaxLength} caracteres.";

        var category = Get("categoria");
        if (category.Length == 0)
            category = type == TransactionType.Income ? DefaultIncomeCategory : DefaultExpenseCategory;
        if (category.Length > Category.NameMaxLength)
            return $"Nome de categoria com mais de {Category.NameMaxLength} caracteres.";

        row = new CsvTransactionRow(line, type, amount, date, description, category);
        return null;
    }

    private static bool TryParseDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text, ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);

    private static bool TryParseType(string text, out TransactionType type)
    {
        type = default;
        if (new[] { "receita", "entrada", "credito", "crédito", "income", "c" }.Any(t => TextSearch.EqualsLoose(t, text)))
            type = TransactionType.Income;
        else if (new[] { "despesa", "saida", "saída", "debito", "débito", "expense", "d" }.Any(t => TextSearch.EqualsLoose(t, text)))
            type = TransactionType.Expense;
        else
            return false;
        return true;
    }

    /// <summary>
    /// Accepts "1234,56", "1.234,56", "1234.56", "R$ 1.234,56" and "-50,00".
    /// With both separators the last one is the decimal mark; a lone '.' is decimal only if 1–2 digits follow it.
    /// </summary>
    public static bool TryParseAmount(string text, out decimal amount)
    {
        amount = 0;
        var s = text.Replace("R$", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "").Replace(" ", "");
        if (s.Length == 0 || !AmountChars().IsMatch(s)) return false;

        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        string normalized;
        if (lastComma >= 0 && lastDot >= 0)
        {
            normalized = lastComma > lastDot
                ? s.Replace(".", "").Replace(',', '.')
                : s.Replace(",", "");
        }
        else if (lastComma >= 0)
        {
            if (s.Count(c => c == ',') > 1) return false;
            normalized = s.Replace(',', '.');
        }
        else if (lastDot >= 0)
        {
            var decimals = s.Length - lastDot - 1;
            normalized = s.Count(c => c == '.') == 1 && decimals <= 2 ? s : s.Replace(".", "");
        }
        else
        {
            normalized = s;
        }

        return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out amount);
    }

    [GeneratedRegex(@"^[+-]?[0-9.,]+$")]
    private static partial Regex AmountChars();
}
