using Microsoft.AspNetCore.Mvc;
using Monica.Tool.Results;

namespace Monica.Core.Extensions;

public static class RESTfulApiExtensions
{
    /// <summary>
    /// Awaits a task result and wraps Monica responses as <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="response">The task that returns an object.</param>
    /// <param name="controller">The controller instance. Reserved for API symmetry.</param>
    /// <returns>The original result or an <see cref="ObjectResult"/> for Monica responses.</returns>
    public static async Task<object> GetResponse(this Task<object> response, ControllerBase controller)
    {
        var res = await response;
        if (res is IResultEnvelope serviceResponse)
        {
            return new ObjectResult(serviceResponse)
            {
                StatusCode = (int?)serviceResponse.ToHttpStatusCode()
            };
        }
        return res;
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
    {
        var res = await response as IResultEnvelope;
        return new ObjectResult(res)
        {
            StatusCode = (int?)res.ToHttpStatusCode()
        };
    }

    /// <summary>
    /// Wraps the Monica response as <see cref="ObjectResult"/>.
    /// </summary>
    /// <typeparam name="T">The Monica response type.</typeparam>
    /// <param name="response">The Monica response instance.</param>
    /// <param name="controller">The controller instance. Reserved for API symmetry.</param>
    /// <returns>An <see cref="ObjectResult"/> with the response payload and HTTP status code.</returns>
    public static ObjectResult GetResponse<T>(this T response, ControllerBase controller)
        where T : IResultEnvelope
    {
        return new ObjectResult(response)
        {
            StatusCode = (int?)response.ToHttpStatusCode()
        };
    }
}
