using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Core.Domain;

namespace FinanceTracker.Core.Persistence;

/// <summary>Where the whole <see cref="FinanceData"/> document lives (browser storage, memory in tests...).</summary>
public interface IFinanceDataStore
{
    /// <returns>The saved data, or <c>null</c> on first use.</returns>
    Task<FinanceData?> LoadAsync(CancellationToken ct = default);

    Task SaveAsync(FinanceData data, CancellationToken ct = default);
}

/// <summary>JSON format shared by storage and backup files.</summary>
public static class FinanceDataSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(FinanceData data) => JsonSerializer.Serialize(data, Options);

    /// <exception cref="DomainException">The text is not a valid backup of a supported version.</exception>
    public static FinanceData Deserialize(string json)
    {
        FinanceData? data;
        try
        {
            data = JsonSerializer.Deserialize<FinanceData>(json, Options);
        }
        catch (JsonException)
        {
            throw new DomainException("Arquivo inválido: não é um backup do Finance Tracker.");
        }

        if (data is null)
            throw new DomainException("Arquivo inválido: backup vazio.");
        if (data.SchemaVersion > FinanceData.CurrentSchemaVersion)
            throw new DomainException("Este backup foi criado por uma versão mais nova do aplicativo.");

        return data;
    }
}

/// <summary>
/// Keeps data as JSON text in memory. Used by tests, and it round-trips through the real
/// serializer so serialization bugs show up there too.
/// </summary>
public sealed class InMemoryFinanceDataStore : IFinanceDataStore
{
    public string? Json { get; private set; }

    public Task<FinanceData?> LoadAsync(CancellationToken ct = default) =>
        Task.FromResult(Json is null ? null : FinanceDataSerializer.Deserialize(Json));

    public Task SaveAsync(FinanceData data, CancellationToken ct = default)
    {
        Json = FinanceDataSerializer.Serialize(data);
        return Task.CompletedTask;
    }
}
