using System.Text;
using System.Text.Json;
using Dapr;
using Microsoft.Extensions.Configuration;

namespace Monica.Dapr.Services.Support;

/// <summary>
/// Uses Dapr's raw HTTP query endpoint so state documents remain independent from the SDK client's wire JSON options.
/// </summary>
internal sealed class DaprStateQueryClient : IDaprStateQueryClient, IDisposable
{
    private readonly HttpClient _httpClient;

    public DaprStateQueryClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClient = DaprDefaults.CreateDefaultHttpClient(httpClientFactory, configuration);
    }

    public async Task<IReadOnlyDictionary<string, string?>> QueryAsync(
        string stateStoreName,
        string jsonQuery,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateStoreName);
        ArgumentNullException.ThrowIfNull(jsonQuery);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1.0-alpha1/state/{Uri.EscapeDataString(stateStoreName)}/query")
        {
            Content = new StringContent(jsonQuery, Encoding.UTF8, "application/json")
        };
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Dapr state query failed with HTTP {(int)response.StatusCode} ({response.StatusCode}): {detail}",
                inner: null,
                response.StatusCode);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        // The transport envelope must not impose a shallower limit than a selected durable profile. The profile
        // deserializer remains authoritative for each extracted state document.
        using var document = await JsonDocument.ParseAsync(
            responseStream,
            new JsonDocumentOptions { MaxDepth = int.MaxValue },
            cancellationToken);
        if (!document.RootElement.TryGetProperty("results", out var resultsElement)
            || resultsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Dapr state-query response does not contain a results array.");
        }

        var results = new Dictionary<string, string?>(StringComparer.Ordinal);
        var failedKeys = new List<string>();
        foreach (var item in resultsElement.EnumerateArray())
        {
            var key = item.GetProperty("key").GetString()
                ?? throw new JsonException("Dapr state-query result contains a null key.");
            if (item.TryGetProperty("error", out var errorElement)
                && !string.IsNullOrWhiteSpace(errorElement.GetString()))
            {
                failedKeys.Add(key);
                continue;
            }

            string? data = null;
            if (item.TryGetProperty("data", out var dataElement)
                && dataElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                data = dataElement.GetRawText();
            }

            if (!results.TryAdd(key, data))
            {
                throw new JsonException($"Dapr state-query response contains duplicate key '{key}'.");
            }
        }

        if (failedKeys.Count != 0)
        {
            throw new InvalidOperationException(
                $"Dapr state query returned errors for keys: {string.Join(", ", failedKeys)}.");
        }

        return results;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
