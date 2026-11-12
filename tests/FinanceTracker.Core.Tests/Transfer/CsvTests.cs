using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Transfer;

namespace FinanceTracker.Core.Tests.Transfer;

public class CsvTests
{
    [Fact]
    public void Quoted_fields_keep_separators_quotes_and_line_breaks()
    {
        var text = "a;b;c\r\n\"x;y\";\"diz \"\"oi\"\"\";\"linha 1\nlinha 2\"\r\n";

        var rows = Csv.Read(text, ';');

        Assert.Equal(2, rows.Count);
        Assert.Equal(["x;y", "diz \"oi\"", "linha 1\nlinha 2"], rows[1].Fields);
    }

    [Fact]
    public void Write_then_read_round_trips()
    {
        IReadOnlyList<string>[] original = [["a", "b;c", "d\"e"], ["1", "", " espaço "]];

        var rows = Csv.Read(Csv.Write(original), ';');

        Assert.Equal(original.Select(r => r.ToList()), rows.Select(r => r.Fields));
    }

    [Fact]
    public void Line_numbers_point_to_where_each_row_starts()
    {
        var rows = Csv.Read("h\n\"a\nb\"\nc\n", ';');

        Assert.Equal([1, 2, 4], rows.Select(r => r.Line));
    }

    [Theory]
    [InlineData("data;valor;descricao", ';')]
    [InlineData("data,valor,descricao", ',')]
    [InlineData("data\tvalor\tdescricao", '\t')]
    public void Separator_is_detected_from_the_header(string header, char expected) =>
        Assert.Equal(expected, Csv.DetectSeparator(header + "\n1;2"));

    [Fact]
    public void Excel_byte_order_mark_is_ignored()
    {
        var rows = Csv.Read("﻿data;valor", ';');

        Assert.Equal("data", rows[0].Fields[0]);
    }
}

public class TransactionCsvTests
{
    [Theory]
    [InlineData("1234,56", 1234.56)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("R$ 1.234,56", 1234.56)]
    [InlineData("-50,00", -50)]
    [InlineData("1.234", 1234)]
    [InlineData("10", 10)]
    [InlineData("0,5", 0.5)]
    public void Amounts_in_brazilian_and_international_formats(string text, decimal expected)
    {
        Assert.True(TransactionCsv.TryParseAmount(text, out var amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1,2,3")]
    [InlineData("12a")]
    public void Invalid_amounts_are_rejected(string text) =>
        Assert.False(TransactionCsv.TryParseAmount(text, out _));

    [Fact]
    public void Parse_reads_a_typical_file()
    {
        var csv = """
                  data;tipo;valor;categoria;descricao
                  05/11/2026;Receita;6.200,00;Salário;Salário de novembro
                  2026-11-07;despesa;89,90;Alimentação;"Mercado; feira"
                  """;

        var result = TransactionCsv.Parse(csv);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Rows.Count);
        var expense = result.Rows[1];
        Assert.Equal(TransactionType.Expense, expense.Type);
        Assert.Equal(89.90m, expense.Amount);
        Assert.Equal(new DateOnly(2026, 11, 7), expense.Date);
        Assert.Equal("Mercado; feira", expense.Description);
        Assert.Equal("Alimentação", expense.CategoryName);
        Assert.Equal(3, expense.Line);
    }

    [Fact]
    public void Columns_can_be_in_any_order_with_english_or_accented_names()
    {
        var csv = "Description,Amount,Date,Type\nCafé,12.50,2026-11-01,expense";

        var row = Assert.Single(TransactionCsv.Parse(csv).Rows);

        Assert.Equal("Café", row.Description);
        Assert.Equal(12.50m, row.Amount);
        Assert.Equal(TransactionCsv.DefaultExpenseCategory, row.CategoryName); // no category column
    }

    [Fact]
    public void Without_a_type_column_the_sign_decides()
    {
        var csv = "data;valor;descricao\n01/11/2026;-30,00;Padaria\n02/11/2026;100,00;Pix recebido";

        var rows = TransactionCsv.Parse(csv).Rows;

        Assert.Equal(TransactionType.Expense, rows[0].Type);
        Assert.Equal(30m, rows[0].Amount);
        Assert.Equal(TransactionType.Income, rows[1].Type);
    }

    [Fact]
    public void Each_bad_line_gets_its_own_error_and_good_lines_still_parse()
    {
        var csv = """
                  data;tipo;valor;categoria;descricao
                  31/02/2026;Despesa;10,00;Lazer;Data impossível
                  01/11/2026;Despesa;abc;Lazer;Valor ruim
                  01/11/2026;Talvez;10,00;Lazer;Tipo ruim
                  01/11/2026;Despesa;0,00;Lazer;Zero
                  01/11/2026;Despesa;1,234;Lazer;Três casas
                  01/11/2026;Despesa;10,00;Lazer;
                  01/11/2026;Despesa;10,00;Lazer;Ok
                  """;

        var result = TransactionCsv.Parse(csv);

        Assert.Equal([2, 3, 4, 5, 6, 7], result.Errors.Select(e => e.Line));
        Assert.Contains("Data inválida", result.Errors[0].Message);
        Assert.Contains("Valor inválido", result.Errors[1].Message);
        Assert.Contains("Tipo inválido", result.Errors[2].Message);
        Assert.Equal(8, Assert.Single(result.Rows).Line);
    }

    [Fact]
    public void Missing_required_columns_are_reported_on_the_header()
    {
        var result = TransactionCsv.Parse("nome;quantia\nx;1");

        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.Line);
        Assert.Contains("data", error.Message);
        Assert.Contains("descricao", error.Message);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Empty_file_is_an_error() =>
        Assert.Contains("vazio", Assert.Single(TransactionCsv.Parse("  ").Errors).Message);

    [Fact]
    public async Task Export_then_parse_gives_back_the_same_transactions()
    {
        var app = new TestApp();
        await app.IncomeAsync(6200m, description: "Salário");
        await app.ExpenseAsync(1234.56m, description: "Compra com \"aspas\"; e ponto e vírgula");

        var csv = await app.Backup.ExportCsvAsync();
        var rows = TransactionCsv.Parse(csv).Rows;

        Assert.StartsWith("data;tipo;valor;categoria;descricao", csv);
        Assert.Contains("1234,56", csv);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Description == "Compra com \"aspas\"; e ponto e vírgula" && r.Amount == 1234.56m);
    }
}
