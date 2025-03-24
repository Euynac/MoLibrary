using BuildingBlocksPlatform.DomainDrivenDesign.Interfaces;
using BuildingBlocksPlatform.SeedWork;
using MoLibrary.DependencyInjection.AppInterfaces;

namespace BuildingBlocksPlatform.DomainDrivenDesign;

public abstract class MoDomainService : IMoDomainService
{
    public IMoServiceProvider MoProvider { get; set; }

    public IServiceProvider ServiceProvider => MoProvider.ServiceProvider;

}

public abstract class MoDomainService<TService> : MoDomainService, IMoServiceProviderInjector where TService : MoDomainService<TService>
{
   
}