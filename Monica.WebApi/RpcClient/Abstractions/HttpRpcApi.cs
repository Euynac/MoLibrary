using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.DependencyInjection.Abstractions;
using Monica.WebApi.RpcClient.Extensions;

namespace Monica.WebApi.RpcClient.Abstractions;

/// <summary>
/// Provides the shared HTTP transport behavior for generated RPC clients.
/// </summary>
/// <param name="serviceProvider">The cached service provider owned by the current Monica host.</param>
/// <param name="httpClient">The HTTP client configured for the remote RPC domain.</param>
public abstract class HttpRpcApi(ICachedServiceProvider serviceProvider, HttpClient httpClient) : RpcApi(serviceProvider)
{
    private readonly IJsonSerializerOptionsProvider _serializerOptionsProvider = serviceProvider
        .GetRequiredService<IJsonSerializerOptionsProvider>();

    protected readonly HttpClient HttpClient = httpClient;

    /// <summary>
    /// Creates a relative request URI using the date-time wire policy owned by the current Monica host.
    /// </summary>
    /// <typeparam name="TRequest">The request contract type.</typeparam>
    /// <param name="request">The request whose route and query properties should be serialized.</param>
    /// <param name="routeTemplate">The complete route template.</param>
    /// <param name="includeQueryString">Whether non-route properties should be appended to the query string.</param>
    /// <returns>The escaped relative request URI.</returns>
    protected string CreateRequestUri<TRequest>(
        TRequest request,
        string routeTemplate,
        bool includeQueryString)
    {
        return request.BuildApiRequestUri(
            routeTemplate,
            includeQueryString,
            _serializerOptionsProvider.DateTimeFormat);
    }

    /// <summary>
    /// Creates JSON request content using the serializer options owned by the current Monica host.
    /// </summary>
    /// <typeparam name="TRequest">The request contract type.</typeparam>
    /// <param name="request">The request to serialize.</param>
    /// <returns>HTTP content containing the serialized request.</returns>
    /// <remarks>
    /// Override this method when a transport requires a different JSON media type or content implementation.
    /// </remarks>
    protected virtual HttpContent CreateJsonRequestContent<TRequest>(TRequest request)
    {
        return JsonContent.Create(request, options: _serializerOptionsProvider.SerializerOptions);
    }
}
