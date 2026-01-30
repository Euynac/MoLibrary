using Monica.DependencyInjection.AppInterfaces;
using Monica.DomainDrivenDesign.Interfaces;

namespace Monica.DomainDrivenDesign;

public abstract class MoDomainService : IMoDomainService
{
}

public abstract class MoDomainService<TSelf> : MoDomainService, ICachedServiceProviderInjector where TSelf : MoDomainService<TSelf>
{
    public ICachedServiceProvider ServiceProvider { get; set; } = null!;
}