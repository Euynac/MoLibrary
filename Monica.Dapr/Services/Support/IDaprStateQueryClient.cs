namespace Monica.Dapr.Services.Support;

/// <summary>
/// Reads Dapr state-query results without passing durable documents through the Dapr client's API-wire serializer.
/// </summary>
internal interface IDaprStateQueryClient
{
    /// <summary>
    /// Executes one Dapr state query and returns each result as its original JSON document.
    /// </summary>
    /// <param name="stateStoreName">The Dapr component name.</param>
    /// <param name="jsonQuery">The provider-protocol query document.</param>
    /// <param name="cancellationToken">Stops the sidecar request.</param>
    /// <returns>Raw JSON documents keyed by state key; a null value represents a null state document.</returns>
    Task<IReadOnlyDictionary<string, string?>> QueryAsync(
        string stateStoreName,
        string jsonQuery,
        CancellationToken cancellationToken);
}
