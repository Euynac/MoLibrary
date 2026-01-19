using MoLibrary.DependencyInjection.AppInterfaces;

namespace MoLibrary.DomainDrivenDesign.AutoController.MoRpc;

public abstract class MoRpcApi(ICachedServiceProvider serviceProvider) : IMoRpcApi
{
    protected ICachedServiceProvider CachedServiceProvider { get; } = serviceProvider;
}