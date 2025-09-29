using MoLibrary.DependencyInjection.AppInterfaces;

namespace MoLibrary.Framework.Features.MoRpc;

public abstract class MoRpcApi(IMoServiceProvider provider) : IMoRpcApi
{
    protected IServiceProvider ServiceProvider => provider.ServiceProvider;
}