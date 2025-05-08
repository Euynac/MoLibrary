using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.DomainDrivenDesign.Modules;

public static class ModuleDomainDrivenDesignBuilderExtensions
{
    public static ModuleDomainDrivenDesignGuide AddMoModuleDomainDrivenDesign(this IServiceCollection services, Action<ModuleDomainDrivenDesignOption>? action = null)
    {
        return new ModuleDomainDrivenDesignGuide().Register(action);
    }
}