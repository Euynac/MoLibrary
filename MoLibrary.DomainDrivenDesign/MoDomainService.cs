using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.DomainDrivenDesign.Interfaces;

namespace MoLibrary.DomainDrivenDesign;

public abstract class MoDomainService : IMoDomainService
{
    public IMoServiceProvider MoProvider { get; set; }

    public IServiceProvider ServiceProvider => MoProvider.ServiceProvider;

}

public abstract class MoDomainService<TSelf> : MoDomainService, IMoServiceProviderInjector where TSelf : MoDomainService<TSelf>
{
   
}