using Bunit;
using FinanceTracker.Core.Services;
using FinanceTracker.Web.Pages.Budgets;
using FinanceTracker.Web.Pages.Categories;
using FinanceTracker.Web.Pages.Dashboard;
using FinanceTracker.Web.Pages.Transactions;

namespace FinanceTracker.Web.Tests;

public class PageTests : WebTestContext
{
    [Fact]
    public void Dashboard_welcomes_a_new_user()
    {
        var cut = Render<DashboardPage>();

        cut.WaitForAssertion(() => Assert.Contains("Bem-vindo ao Finance Tracker", cut.Markup));
    }

    [Fact]
    public async Task Dashboard_shows_the_month_numbers_and_charts()
    {
        await IncomeAsync(5000m);
        await ExpenseAsync(1200m, "Moradia", "Aluguel");
        await ExpenseAsync(300m, "Alimentação", "Mercado");

        var cut = Render<DashboardPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("R$ 3.500,00", cut.Markup.Replace(' ', ' ')); // balance
            Assert.Contains("Você guardou <b>70%</b>", cut.Markup);
            Assert.Equal(2, cut.FindAll("circle.donut-slice").Count);
            Assert.Equal(6, cut.FindAll("g.trend-column").Count);
            Assert.Contains("Aluguel", cut.Markup);
        });
    }

    [Fact]
    public async Task Transactions_page_lists_the_month_with_totals()
    {
        await IncomeAsync(5000m);
        await ExpenseAsync(89.9m, description: "Feira");

        var cut = Render<TransactionsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Feira", cut.Markup);
            Assert.Contains("− R$ 89,90", cut.Markup.Replace(' ', ' '));
            Assert.Contains("2 transações", cut.Markup);
        });
    }

    [Fact]
    public async Task Categories_in_use_cannot_be_deleted()
    {
        await ExpenseAsync(10m, "Alimentação");

        var cut = Render<CategoriesPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.Find("button[aria-label='Excluir Alimentação']").HasAttribute("disabled"));
            Assert.False(cut.Find("button[aria-label='Excluir Lazer']").HasAttribute("disabled"));
        });
    }

    [Fact]
    public async Task Budgets_page_flags_warning_and_exceeded_limits()
    {
        await Service<BudgetService>().SetAsync((await CategoryAsync("Alimentação")).Id, 400m);
        await Service<BudgetService>().SetAsync((await CategoryAsync("Lazer")).Id, 100m);
        await ExpenseAsync(450m, "Alimentação");
        await ExpenseAsync(85m, "Lazer");

        var cut = Render<BudgetsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Estourado", cut.Markup);
            Assert.Contains("Acima em R$ 50,00", cut.Markup.Replace(' ', ' '));
            Assert.Contains("Atenção", cut.Markup);
            Assert.Contains("1 estourado", cut.Markup);
            Assert.Contains("1 em alerta", cut.Markup);
        });
    }
}
