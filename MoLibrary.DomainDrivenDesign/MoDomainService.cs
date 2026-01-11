using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.DomainDrivenDesign.Interfaces;

namespace MoLibrary.DomainDrivenDesign;

public abstract class MoDomainService : IMoDomainService
{
}

public abstract class MoDomainService<TSelf> : MoDomainService, ICachedServiceProviderInjector where TSelf : MoDomainService<TSelf>
{
    public ICachedServiceProvider ServiceProvider { get; set; } = null!;
}