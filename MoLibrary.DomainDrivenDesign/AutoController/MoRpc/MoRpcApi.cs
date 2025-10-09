using MoLibrary.DependencyInjection.AppInterfaces;

namespace MoLibrary.DomainDrivenDesign.AutoController.MoRpc;

public abstract class MoRpcApi(IMoServiceProvider provider) : IMoRpcApi
{
    protected IServiceProvider ServiceProvider => provider.ServiceProvider;
}