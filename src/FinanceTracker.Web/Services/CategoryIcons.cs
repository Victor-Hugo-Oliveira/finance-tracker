using MudBlazor;

namespace FinanceTracker.Web.Services;

/// <summary>
/// Maps the icon names stored with each category to MudBlazor's Material icons.
/// The domain stores plain names so the data doesn't depend on a UI library.
/// </summary>
public static class CategoryIcons
{
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        ["work"] = Icons.Material.Filled.Work,
        ["laptop"] = Icons.Material.Filled.Laptop,
        ["trending_up"] = Icons.Material.Filled.TrendingUp,
        ["add_circle"] = Icons.Material.Filled.AddCircle,
        ["home"] = Icons.Material.Filled.Home,
        ["restaurant"] = Icons.Material.Filled.Restaurant,
        ["directions_car"] = Icons.Material.Filled.DirectionsCar,
        ["favorite"] = Icons.Material.Filled.Favorite,
        ["school"] = Icons.Material.Filled.School,
        ["celebration"] = Icons.Material.Filled.Celebration,
        ["subscriptions"] = Icons.Material.Filled.Subscriptions,
        ["more_horiz"] = Icons.Material.Filled.MoreHoriz,
        ["shopping_cart"] = Icons.Material.Filled.ShoppingCart,
        ["pets"] = Icons.Material.Filled.Pets,
        ["flight"] = Icons.Material.Filled.Flight,
        ["checkroom"] = Icons.Material.Filled.Checkroom,
        ["fitness_center"] = Icons.Material.Filled.FitnessCenter,
        ["card_giftcard"] = Icons.Material.Filled.CardGiftcard,
        ["savings"] = Icons.Material.Filled.Savings,
        ["receipt"] = Icons.Material.Filled.Receipt
    };

    public static string Get(string? name) =>
        name is not null && All.TryGetValue(name, out var icon) ? icon : Icons.Material.Filled.Label;
}
