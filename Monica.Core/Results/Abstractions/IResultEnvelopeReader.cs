namespace Monica.Core.Results.Abstractions;

/// <summary>
/// Resolves remote HTTP responses with the serializer and diagnostic policy owned by one Monica host.
/// </summary>
public interface IResultEnvelopeReader
{
    /// <summary>
    /// Reads a remote HTTP response into a Monica result envelope.
    /// </summary>
    /// <typeparam name="TResponse">The result envelope type to deserialize.</typeparam>
    /// <param name="httpResponse">The remote response to consume.</param>
    /// <param name="cancellationToken">A token that cancels response reading and deserialization.</param>
    /// <returns>The resolved envelope, or an internal-error envelope when the payload is invalid.</returns>
    Task<TResponse> ReadRemoteResponse<TResponse>(
        HttpResponseMessage httpResponse,
        CancellationToken cancellationToken = default)
        where TResponse : class, IResultEnvelope, new();
}
