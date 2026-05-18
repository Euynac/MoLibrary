using System.Net.Http;

namespace Monica.UnitTests.Hosting;

/// <summary>
/// Test HTTP client factory that fails unless a test explicitly registers a named client.
/// </summary>
public sealed class TestHttpClientFactory : IHttpClientFactory
{
    private readonly IReadOnlyDictionary<string, HttpClient> _clients;

    /// <summary>
    /// Creates a factory with no configured clients.
    /// </summary>
    public TestHttpClientFactory()
        : this(new Dictionary<string, HttpClient>())
    {
    }

    /// <summary>
    /// Creates a factory with explicitly configured named clients.
    /// </summary>
    public TestHttpClientFactory(IReadOnlyDictionary<string, HttpClient> clients)
    {
        _clients = clients;
    }

    /// <inheritdoc />
    public HttpClient CreateClient(string name)
    {
        return _clients.TryGetValue(name, out var client)
            ? client
            : throw new InvalidOperationException(
                $"No test HttpClient named '{name}' is registered. Register one through a test seam before making HTTP calls.");
    }
}
