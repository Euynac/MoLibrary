using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Monica.Tool.Extensions;
using Monica.Tool.Results;

namespace Monica.Core.Extensions;

/// <summary>
/// Provides a shared endpoint-filter workaround because ASP.NET Core does not support global endpoint filters yet.
/// See aspnetcore issue <c>#43237</c>.
/// </summary>
public static class MinimalApiExtensions
{
    private static Action<RouteHandlerBuilder>? _sharedFilterAction;
    /// <summary>
    /// Registers a filter of type <typeparamref name="TFilterType" /> onto the route handler.
    /// </summary>
    /// <typeparam name="TFilterType">The type of the <see cref="T:Microsoft.AspNetCore.Http.IEndpointFilter" /> to register.</typeparam>
    /// <returns>A <see cref="T:Microsoft.AspNetCore.Builder.RouteHandlerBuilder" /> that can be used to further customize the route handler.</returns>
    public static void AddSharedEndpointFilter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFilterType>()
        where TFilterType : IEndpointFilter
    {
        ActionExtensions.WrapAction(ref _sharedFilterAction, handlerBuilder => handlerBuilder.AddEndpointFilter<RouteHandlerBuilder, TFilterType>());
    }

    /// <summary>
    /// Registers shared filter of type onto the route handler.
    /// </summary>
    /// <param name="builder">The <see cref="T:Microsoft.AspNetCore.Builder.RouteHandlerBuilder" />.</param>
    /// <returns>A <see cref="T:Microsoft.AspNetCore.Builder.RouteHandlerBuilder" /> that can be used to further customize the route handler.</returns>
    public static RouteHandlerBuilder UseSharedEndpointFilter(
        this RouteHandlerBuilder builder)
    {
        _sharedFilterAction?.Invoke(builder);
        return builder;
    }

    /// <summary>
    /// Wraps an <see cref="IResultEnvelope"/> as a Minimal API JSON result.
    /// </summary>
    /// <typeparam name="T">The response type.</typeparam>
    /// <param name="response">The Monica response instance.</param>
    /// <returns>An <see cref="IResult"/> with the response payload and HTTP status code.</returns>
    public static IResult GetResponse<T>(this T response) where T : IResultEnvelope
    {
        return Results.Json(response, statusCode: (int?)response.ToHttpStatusCode());
    }

    /// <summary>
    /// Wraps an <see cref="IResultEnvelope"/> as a projected external API JSON result.
    /// </summary>
    /// <typeparam name="T">The response type.</typeparam>
    /// <param name="response">The Monica response instance.</param>
    /// <param name="httpContext">The current HTTP context. Reserved for API symmetry.</param>
    /// <returns>An <see cref="IResult"/> with the projected payload and HTTP status code.</returns>
    public static IResult GetProjectedResponse<T>(this T response, HttpContext httpContext) where T : IResultEnvelope
    {
        var payload = Mo.Options.ResultProjector?.ProjectToObject(response) ?? response;
        return Results.Json(payload, statusCode: (int?)response.ToHttpStatusCode());
    }

    /// <summary>
    /// Awaits a task result and wraps the Monica response as a projected external API JSON result.
    /// </summary>
    /// <typeparam name="T">The Monica response type.</typeparam>
    /// <param name="response">The task that returns the Monica response.</param>
    /// <param name="httpContext">The current HTTP context. Reserved for API symmetry.</param>
    /// <returns>An <see cref="IResult"/> with the projected payload and HTTP status code.</returns>
    public static async Task<IResult> GetProjectedResponse<T>(this Task<T> response, HttpContext httpContext)
        where T : IResultEnvelope
    {
        return (await response).GetProjectedResponse(httpContext);
    }
}
