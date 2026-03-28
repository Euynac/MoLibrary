using System.Text.Json;

namespace Monica.Core.Results;

/// <summary>
/// Converts Monica result envelopes between internal models and external JSON representations.
/// </summary>
public interface IResultProjector
{
    /// <summary>
    /// Outbound projection from a Monica result envelope to a custom API payload.
    /// </summary>
    /// <param name="response">The Monica result envelope.</param>
    /// <returns>The payload that should be serialized to the API response body.</returns>
    object Project(IResultEnvelope response);

    /// <summary>
    /// Inbound resolution from remote JSON into a Monica result envelope.
    /// </summary>
    /// <typeparam name="TResponse">The Monica envelope type to materialize.</typeparam>
    /// <param name="json">The remote JSON payload.</param>
    /// <param name="options">The serializer options configured for the result module.</param>
    /// <returns>The resolved Monica result envelope.</returns>
    TResponse Resolve<TResponse>(string json, JsonSerializerOptions options)
        where TResponse : class, IResultEnvelope, new();
}
