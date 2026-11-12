using FinanceTracker.Core.Domain;

namespace FinanceTracker.Core.Transfer;

/// <summary>
/// Six months of believable finances for trying the app (or showing it in a portfolio).
/// Deterministic: the same <c>today</c> always produces the same data.
/// </summary>
public static class SampleData
{
    public const int Months = 6;

    public static FinanceData Create(DateOnly today, DateTime createdAtUtc)
    {
        var data = FinanceData.CreateDefault();
        var random = new Random(2026);
        Category Cat(string name) => data.Categories.Single(c => c.Name == name);

        void Add(string category, decimal amount, DateOnly date, string description)
        {
            if (date > today) return; // nothing in the future
            var c = Cat(category);
            data.Transactions.Add(new Transaction(c.Type, decimal.Round(amount, 2), date, description, c.Id, createdAtUtc));
        }

        decimal Around(decimal value, decimal spread) => value + (decimal)(random.NextDouble() * 2 - 1) * spread;

        var current = YearMonth.From(today);
        for (var i = Months - 1; i >= 0; i--)
        {
            var month = current.AddMonths(-i);
            DateOnly Day(int day) => new(month.Year, month.Month, Math.Min(day, month.LastDay.Day));

            // Income
            Add("Salário", 6200m, Day(5), "Salário");
            if (random.NextDouble() < 0.5) Add("Freelance", Around(1500m, 700m), Day(random.Next(10, 26)), "Projeto freelance");
            if (month.Month % 3 == 0) Add("Investimentos", Around(320m, 80m), Day(15), "Rendimentos do CDB");

            // Fixed expenses
            Add("Moradia", 1850m, Day(10), "Aluguel");
            Add("Moradia", Around(190m, 40m), Day(12), "Conta de luz");
            Add("Moradia", 120m, Day(12), "Internet");
            Add("Assinaturas", 55.90m, Day(3), "Streaming de vídeo");
            Add("Assinaturas", 21.90m, Day(3), "Streaming de música");
            Add("Educação", 349m, Day(8), "Curso de inglês");

            // Variable expenses
            for (var w = 0; w < 4; w++)
                Add("Alimentação", Around(210m, 60m), Day(2 + w * 7), "Supermercado");
            for (var k = 0; k < 3; k++)
                Add("Alimentação", Around(55m, 25m), Day(random.Next(1, 29)), "Restaurante");
            for (var k = 0; k < 4; k++)
                Add("Transporte", Around(48m, 18m), Day(random.Next(1, 29)), k % 2 == 0 ? "Combustível" : "Aplicativo de transporte");
            Add("Lazer", Around(160m, 90m), Day(random.Next(14, 28)), "Cinema e passeio");
            if (random.NextDouble() < 0.6) Add("Saúde", Around(95m, 50m), Day(random.Next(1, 29)), "Farmácia");
            if (i == 3) Add("Lazer", 1290m, Day(20), "Viagem de fim de semana");
            if (i == 1) Add("Saúde", 380m, Day(17), "Consulta médica");
        }

        data.Budgets.Add(new Budget(Cat("Alimentação").Id, 1100m));
        data.Budgets.Add(new Budget(Cat("Transporte").Id, 250m));
        data.Budgets.Add(new Budget(Cat("Lazer").Id, 300m));
        data.Budgets.Add(new Budget(Cat("Assinaturas").Id, 100m));
        return data;
    }
}
