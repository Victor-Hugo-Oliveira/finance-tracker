using FinanceTracker.Core.Domain;
using FinanceTracker.Core.Services;

namespace FinanceTracker.Core.Tests.Transfer;

public class ImportServiceTests
{
    private readonly TestApp _app = new();

    private const string Header = "data;tipo;valor;categoria;descricao\n";

    [Fact]
    public async Task Preview_does_not_change_anything()
    {
        await _app.Imports.PreviewAsync(Header + "01/11/2026;Despesa;10,00;Pets;Ração");

        Assert.Empty(await _app.Transactions.QueryAsync());
        Assert.DoesNotContain(await _app.Categories.GetAllAsync(), c => c.Name == "Pets");
    }

    [Fact]
    public async Task Import_adds_valid_rows_and_matches_categories_ignoring_accents()
    {
        var preview = await _app.Imports.PreviewAsync(Header +
            "01/11/2026;Despesa;10,00;alimentacao;Pão\n" +
            "02/11/2026;Receita;100,00;SALÁRIO;Bônus");

        var count = await _app.Imports.ImportAsync(preview);

        Assert.Equal(2, count);
        Assert.Empty(preview.NewCategories);
        var food = await _app.CategoryAsync("Alimentação");
        Assert.Contains(await _app.Transactions.QueryAsync(), t => t.Description == "Pão" && t.CategoryId == food.Id);
        Assert.Equal(100m, preview.TotalIncome);
        Assert.Equal(10m, preview.TotalExpenses);
    }

    [Fact]
    public async Task Missing_categories_are_listed_once_and_created_on_import()
    {
        var preview = await _app.Imports.PreviewAsync(Header +
            "01/11/2026;Despesa;10,00;Pets;Ração\n" +
            "02/11/2026;Despesa;80,00;pets;Veterinário");

        var created = Assert.Single(preview.NewCategories);
        Assert.Equal(("Pets", TransactionType.Expense), (created.Name, created.Type));

        await _app.Imports.ImportAsync(preview);

        var pets = await _app.CategoryAsync("Pets");
        Assert.Equal(2, (await _app.Transactions.QueryAsync(new TransactionFilter { CategoryId = pets.Id })).Count);
    }

    [Fact]
    public async Task Same_name_as_a_category_of_the_other_type_creates_a_new_category()
    {
        var preview = await _app.Imports.PreviewAsync(Header + "01/11/2026;Receita;50,00;Lazer;Venda de ingresso");

        Assert.Empty(preview.Errors);
        Assert.Equal(TransactionType.Income, Assert.Single(preview.NewCategories).Type);
    }

    [Fact]
    public async Task Invalid_lines_are_reported_and_skipped_while_valid_ones_are_imported()
    {
        var preview = await _app.Imports.PreviewAsync(Header +
            "01/11/2026;Despesa;10,00;Lazer;Ok\n" +
            "xx;Despesa;10,00;Lazer;Data ruim");

        Assert.Equal(3, Assert.Single(preview.Errors).Line);
        Assert.Equal(1, await _app.Imports.ImportAsync(preview));
    }

    [Fact]
    public async Task Archived_categories_are_rejected_per_line()
    {
        await _app.Categories.ArchiveAsync((await _app.CategoryAsync("Lazer")).Id);

        var preview = await _app.Imports.PreviewAsync(Header + "01/11/2026;Despesa;10,00;Lazer;Cinema");

        Assert.Contains("arquivada", Assert.Single(preview.Errors).Message);
        Assert.Empty(preview.ToImport);
    }

    [Fact]
    public async Task Importing_the_same_file_twice_skips_the_duplicates()
    {
        var csv = Header + "01/11/2026;Despesa;10,00;Lazer;Cinema\n02/11/2026;Despesa;20,00;Lazer;Show";
        await _app.Imports.ImportAsync(await _app.Imports.PreviewAsync(csv));

        var again = await _app.Imports.PreviewAsync(csv);

        Assert.Equal(2, again.Duplicates.Count);
        Assert.Empty(again.ToImport);
        Assert.Equal(0, await _app.Imports.ImportAsync(again));
        Assert.Equal(2, (await _app.Transactions.QueryAsync()).Count);
    }

    [Fact]
    public async Task Identical_lines_in_one_file_are_all_imported()
    {
        var csv = Header + "01/11/2026;Despesa;5,00;Alimentação;Café\n01/11/2026;Despesa;5,00;Alimentação;Café";

        var preview = await _app.Imports.PreviewAsync(csv);

        Assert.Equal(2, preview.ToImport.Count);
        Assert.Empty(preview.Duplicates);
    }

    [Fact]
    public async Task Duplicate_check_counts_existing_copies()
    {
        await _app.ExpenseAsync(5m, "Alimentação", new DateOnly(2026, 11, 1), "Café");

        var preview = await _app.Imports.PreviewAsync(Header +
            "01/11/2026;Despesa;5,00;Alimentação;Café\n01/11/2026;Despesa;5,00;Alimentação;Café");

        Assert.Single(preview.Duplicates);
        Assert.Single(preview.ToImport);
    }

    [Fact]
    public async Task Imported_data_survives_a_reload()
    {
        await _app.Imports.ImportAsync(await _app.Imports.PreviewAsync(Header + "01/11/2026;Despesa;10,00;Pets;Ração"));

        var reloaded = _app.Reload();

        Assert.Single(await reloaded.Transactions.QueryAsync());
        Assert.NotNull(await reloaded.CategoryAsync("Pets"));
    }
}
