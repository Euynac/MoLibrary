using Monica.Core.Results.Services;

namespace Monica.Core.Results;

/// <summary>
/// Remote HTTP helpers for Monica result envelopes.
/// </summary>
public static class ResultRemoteExtensions
{
    /// <summary>
    /// Awaits a remote HTTP response and resolves it as a Monica result envelope.
    /// </summary>
    /// <typeparam name="TResponse">The Monica response type.</typeparam>
    /// <param name="response">The task that returns the HTTP response.</param>
    /// <returns>The resolved Monica result envelope.</returns>
    public static async Task<TResponse> GetResponse<TResponse>(this Task<HttpResponseMessage> response)
        where TResponse : class, IResultEnvelope, new()
        => await ResultEnvelopeProvider.ReadRemoteResponse<TResponse>(await response);
}
