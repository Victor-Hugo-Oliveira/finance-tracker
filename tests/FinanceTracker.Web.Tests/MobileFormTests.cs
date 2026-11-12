using Bunit;
using FinanceTracker.Web.Pages.Transactions;
using FinanceTracker.Web.Services;
using MudBlazor;

namespace FinanceTracker.Web.Tests;

/// <summary>Regression tests for the Phase 6 bug seen on phones (amount typed with a dot, category list off-screen).</summary>
public class MoneyConverterTests
{
    [Theory]
    [InlineData("38.07", 38.07)]    // phone keyboards often only offer a dot
    [InlineData("38,07", 38.07)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1234,5", 1234.5)]
    [InlineData("R$ 10", 10)]
    public void Reads_what_people_type(string text, decimal expected) =>
        Assert.Equal(expected, MoneyConverter.Instance.ConvertBack(text));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_means_no_value(string? text) =>
        Assert.Null(MoneyConverter.Instance.ConvertBack(text));

    [Fact]
    public void Garbage_is_rejected() =>
        Assert.Throws<FormatException>(() => MoneyConverter.Instance.ConvertBack("abc"));

    [Fact]
    public void Values_are_shown_in_brazilian_format() =>
        Assert.Equal("3.807,00", MoneyConverter.Instance.Convert(3807m));
}

public class TransactionDialogTests : WebTestContext
{
    private async Task OpenDialogAsync()
    {
        var dialogs = Service<IDialogService>();
        await Dialogs.InvokeAsync(() => dialogs.ShowAsync<TransactionDialog>("Nova transação"));
        Dialogs.WaitForAssertion(() => Assert.NotEmpty(Dialogs.FindAll("input")));
    }

    [Fact]
    public async Task Amount_typed_with_a_dot_is_kept_as_cents()
    {
        await OpenDialogAsync();

        var amount = Dialogs.Find("input[inputmode='decimal']");
        amount.Change("38.07");

        Dialogs.WaitForAssertion(() => Assert.Equal("38,07", Dialogs.Find("input[inputmode='decimal']").GetAttribute("value")));
    }

    [Fact]
    public async Task Category_list_opens_aligned_with_the_left_edge_of_the_field()
    {
        await OpenDialogAsync();

        // A list anchored at the field's center grows past the right edge of a phone screen.
        Popovers.WaitForAssertion(() =>
        {
            var lists = Popovers.FindAll(".mud-popover");
            Assert.NotEmpty(lists);
            Assert.All(lists, l =>
            {
                Assert.Contains("mud-popover-anchor-bottom-left", l.ClassName);
                Assert.DoesNotContain("anchor-bottom-center", l.ClassName);
            });
        });
    }
}
