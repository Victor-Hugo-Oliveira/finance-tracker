using FinanceTracker.Core.Domain;

namespace FinanceTracker.Core.Tests.Domain;

public class TransactionTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly DateTime CreatedAt = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Transaction Expense(decimal amount, string description = "Mercado") =>
        new(TransactionType.Expense, amount, new DateOnly(2026, 11, 10), description, CategoryId, CreatedAt);

    [Fact]
    public void Valid_transaction_keeps_values_and_trims_description()
    {
        var t = Expense(150.75m, "  Mercado  ");

        Assert.Equal(150.75m, t.Amount);
        Assert.Equal("Mercado", t.Description);
        Assert.Equal(CreatedAt, t.CreatedAt);
        Assert.Null(t.UpdatedAt);
    }

    [Fact]
    public void Signed_amount_is_negative_for_expenses_and_positive_for_income()
    {
        Assert.Equal(-50m, Expense(50m).SignedAmount);
        Assert.Equal(50m, new Transaction(TransactionType.Income, 50m, new DateOnly(2026, 1, 1), "x", CategoryId, CreatedAt).SignedAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Amount_must_be_positive(decimal amount)
    {
        var ex = Assert.Throws<DomainException>(() => Expense(amount));
        Assert.Equal(nameof(Transaction.Amount), ex.Field);
    }

    [Fact]
    public void Amount_cannot_have_more_than_two_decimal_places()
    {
        var ex = Assert.Throws<DomainException>(() => Expense(10.555m));
        Assert.Equal(nameof(Transaction.Amount), ex.Field);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Description_is_required(string description)
    {
        var ex = Assert.Throws<DomainException>(() => Expense(10m, description));
        Assert.Equal(nameof(Transaction.Description), ex.Field);
    }

    [Fact]
    public void Description_has_a_maximum_length()
    {
        Assert.Throws<DomainException>(() => Expense(10m, new string('a', Transaction.DescriptionMaxLength + 1)));
    }

    [Fact]
    public void Update_changes_values_and_records_when()
    {
        var t = Expense(10m);
        var later = CreatedAt.AddDays(1);

        t.Update(TransactionType.Expense, 20m, new DateOnly(2026, 11, 11), "Feira", CategoryId, later);

        Assert.Equal(20m, t.Amount);
        Assert.Equal("Feira", t.Description);
        Assert.Equal(later, t.UpdatedAt);
    }
}

public class CategoryTests
{
    [Fact]
    public void Color_is_normalized_to_upper_case()
    {
        Assert.Equal("#ABCDEF", new Category("Lazer", TransactionType.Expense, "#abcdef").Color);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#FFF")]
    [InlineData("123456")]
    public void Invalid_colors_are_rejected(string color)
    {
        var ex = Assert.Throws<DomainException>(() => new Category("Lazer", TransactionType.Expense, color));
        Assert.Equal(nameof(Category.Color), ex.Field);
    }

    [Fact]
    public void Name_is_required_and_trimmed()
    {
        Assert.Equal("Pets", new Category("  Pets ", TransactionType.Expense, "#000000").Name);
        Assert.Throws<DomainException>(() => new Category("  ", TransactionType.Expense, "#000000"));
    }

    [Fact]
    public void Archive_and_restore()
    {
        var category = new Category("Pets", TransactionType.Expense, "#000000");

        category.Archive();
        Assert.True(category.IsArchived);

        category.Restore();
        Assert.False(category.IsArchived);
    }
}

public class BudgetTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.001)]
    public void Invalid_limits_are_rejected(decimal limit)
    {
        Assert.Throws<DomainException>(() => new Budget(Guid.NewGuid(), limit));
    }
}

public class FinanceDataTests
{
    [Fact]
    public void Default_data_has_income_and_expense_categories_with_unique_names()
    {
        var data = FinanceData.CreateDefault();

        Assert.Contains(data.Categories, c => c.Type == TransactionType.Income);
        Assert.Contains(data.Categories, c => c.Type == TransactionType.Expense);
        Assert.Equal(data.Categories.Count, data.Categories.Select(c => c.Name).Distinct().Count());
        Assert.Empty(data.Transactions);
        Assert.Empty(data.Budgets);
    }
}
