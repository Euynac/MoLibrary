using Monica.DependencyInjection.Abstractions;

namespace Monica.WebApi.RpcClient.Abstractions;

public abstract class HttpRpcApi(ICachedServiceProvider serviceProvider, HttpClient httpClient) : RpcApi(serviceProvider)
{
    protected readonly HttpClient HttpClient = httpClient;
}
