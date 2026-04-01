using Microsoft.AspNetCore.Http;
using Monica.Core.Results.Abstractions;
using Monica.Core.Results.Services;
// ReSharper disable once CheckNamespace
namespace Monica.Core.Results;

/// <summary>
/// Minimal API helpers for Monica result envelopes.
/// </summary>
public static class ResultMinimalApiExtensions
{
    /// <summary>
    /// Wraps the Monica response as a Minimal API result.
    /// </summary>
    /// <typeparam name="T">The Monica response type.</typeparam>
    /// <param name="response">The Monica response instance.</param>
    /// <returns>The Minimal API result.</returns>
    public static IResult GetResponse<T>(this T response)
        where T : IResultEnvelope
        => ResultEnvelopeProvider.ToMinimalApiResult(response);
}
