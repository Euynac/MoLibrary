using Monica.DependencyInjection.Abstractions;

namespace Monica.WebApi.RpcClient.Abstractions;

public abstract class RpcApi(ICachedServiceProvider serviceProvider) : IRpcApi
{
    protected ICachedServiceProvider CachedServiceProvider { get; } = serviceProvider;
}