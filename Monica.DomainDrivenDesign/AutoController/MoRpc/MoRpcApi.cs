using Monica.DependencyInjection.AppInterfaces;

namespace Monica.DomainDrivenDesign.AutoController.MoRpc;

public abstract class MoRpcApi(ICachedServiceProvider serviceProvider) : IMoRpcApi
{
    protected ICachedServiceProvider CachedServiceProvider { get; } = serviceProvider;
}