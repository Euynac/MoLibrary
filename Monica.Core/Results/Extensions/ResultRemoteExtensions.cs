using Monica.Core.Results.Abstractions;
using Monica.Core.Results.Services;
// ReSharper disable once CheckNamespace
namespace Monica.Core.Results;

/// <summary>
/// Remote HTTP helpers for Monica result envelopes.
/// </summary>
public static class ResultRemoteExtensions
{
    /// <summary>
    /// Awaits a remote HTTP response and resolves it as a Monica result envelope.
    /// </summary>
    /// <typeparam name="TResponse">
    /// The concrete Monica remote result-envelope type.
    /// </typeparam>
    /// <param name="response">The task that returns the HTTP response.</param>
    /// <param name="reader">The host-owned remote envelope reader.</param>
    /// <param name="cancellationToken">A token that cancels response reading and deserialization.</param>
    /// <returns>The resolved Monica result envelope.</returns>
    public static async Task<TResponse> GetResponse<TResponse>(
        this Task<HttpResponseMessage> response,
        IResultEnvelopeReader reader,
        CancellationToken cancellationToken = default)
        where TResponse : class, IRemoteResultEnvelope<TResponse>
    {
        ArgumentNullException.ThrowIfNull(reader);

        return await reader.ReadRemoteResponse<TResponse>(
            await response.WaitAsync(cancellationToken),
            cancellationToken);
    }
}
