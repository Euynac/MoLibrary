using Microsoft.AspNetCore.Mvc;
using Monica.Core.Results.Services;

namespace Monica.Core.Results;

/// <summary>
/// MVC helpers for Monica result envelopes.
/// </summary>
public static class ResMvcExtensions
{
    /// <summary>
    /// Awaits a task result and wraps Monica responses as <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="response">The task that returns an object.</param>
    /// <param name="controller">The controller instance. Reserved for API symmetry.</param>
    /// <returns>The original result or an <see cref="ObjectResult"/> for Monica responses.</returns>
    public static async Task<object> GetResponse(this Task<object> response, ControllerBase controller)
    {
        var result = await response;
        return result is IResultEnvelope serviceResponse
            ? ResultEnvelopeProvider.ToMvcResult(serviceResponse)
            : result;
    }

    /// <summary>
    /// Awaits a task result and wraps the Monica response as <see cref="ObjectResult"/>.
    /// </summary>
    /// <typeparam name="T">The Monica response type.</typeparam>
    /// <param name="response">The task that returns the Monica response.</param>
    /// <param name="controller">The controller instance. Reserved for API symmetry.</param>
    /// <returns>An <see cref="ObjectResult"/> with the response payload and HTTP status code.</returns>
    public static async Task<ObjectResult> GetResponse<T>(this Task<T> response, ControllerBase controller)
        where T : IResultEnvelope
        => ResultEnvelopeProvider.ToMvcResult(await response);

    /// <summary>
    /// Wraps the Monica response as <see cref="ObjectResult"/>.
    /// </summary>
    /// <typeparam name="T">The Monica response type.</typeparam>
    /// <param name="response">The Monica response instance.</param>
    /// <param name="controller">The controller instance. Reserved for API symmetry.</param>
    /// <returns>An <see cref="ObjectResult"/> with the response payload and HTTP status code.</returns>
    public static ObjectResult GetResponse<T>(this T response, ControllerBase controller)
        where T : IResultEnvelope
        => ResultEnvelopeProvider.ToMvcResult(response);
}
