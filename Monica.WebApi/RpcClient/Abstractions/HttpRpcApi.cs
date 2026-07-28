using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.DependencyInjection.Abstractions;

namespace Monica.WebApi.RpcClient.Abstractions;

/// <summary>
/// Provides the shared HTTP transport behavior for generated RPC clients.
/// </summary>
/// <param name="serviceProvider">The cached service provider owned by the current Monica host.</param>
/// <param name="httpClient">The HTTP client configured for the remote RPC domain.</param>
public abstract class HttpRpcApi(ICachedServiceProvider serviceProvider, HttpClient httpClient) : RpcApi(serviceProvider)
{
    protected readonly HttpClient HttpClient = httpClient;

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
        var serializerOptions = CachedServiceProvider
            .GetRequiredService<IJsonSerializerOptionsProvider>()
            .SerializerOptions;

        return JsonContent.Create(request, options: serializerOptions);
    }
}
