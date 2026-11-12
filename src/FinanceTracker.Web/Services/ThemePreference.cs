using Microsoft.JSInterop;

namespace FinanceTracker.Web.Services;

/// <summary>Remembers whether the user picked light or dark mode. No saved choice means "follow the system".</summary>
public sealed class ThemePreference(IJSRuntime js)
{
    public const string StorageKey = "finance-tracker:theme";

    /// <returns><c>true</c> for dark, <c>false</c> for light, <c>null</c> when the user never chose.</returns>
    public async Task<bool?> LoadAsync()
    {
        var value = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        return value switch
        {
            "dark" => true,
            "light" => false,
            _ => null
        };
    }

    public async Task SaveAsync(bool dark) =>
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, dark ? "dark" : "light");
}
